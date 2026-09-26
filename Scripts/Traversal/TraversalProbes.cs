using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace Veilrun.Traversal;

public readonly record struct ProbeHit(bool Valid, Vector3 Point, Vector3 Normal, GodotObject? Collider)
{
    public static readonly ProbeHit None = new(false, Vector3.Zero, Vector3.Zero, null);
}

/// <summary>
/// Result of scanning in front of the runner: the wall face, the ledge top above it (if any),
/// whether the top is deep enough to stand on, and where the far edge is (for vaults).
/// </summary>
public readonly record struct FrontProbe(
    bool HitWall,
    Vector3 WallPoint,
    Vector3 WallNormal,
    float WallDistance,
    bool HasTop,
    float TopY,
    float Height,
    bool Standable,
    bool HasFarEdge,
    float Depth,
    Vector3 FarEdge,
    bool AllowsWallRun)
{
    public static readonly FrontProbe None = default;
}

/// <summary>
/// The single place where traversal talks to the physics world. Every query is counted
/// (per-tick budget, see TraversalTuning.MaxQueriesPerTick) and can be recorded for the
/// debug draw. Query parameter objects are reused to avoid per-tick allocations where the
/// Godot API allows it. Nothing here holds gameplay state: results depend only on the
/// world and the positions passed in, which keeps traversal deterministic and rollbackable.
/// </summary>
public sealed class TraversalProbes
{
    public static readonly StringName WallRunMetaKey = "wallrun";

    private readonly CharacterBody3D _body;
    private readonly PhysicsRayQueryParameters3D _ray = new();
    private readonly PhysicsShapeQueryParameters3D _shapeQuery = new();
    private readonly BoxShape3D _castBox = new();
    private readonly CapsuleShape3D _clearanceCapsule = new();
    private readonly List<DebugSegment> _debug = new(64);

    public TraversalProbes(CharacterBody3D body)
    {
        _body = body;
        var exclude = new Array<Rid> { body.GetRid() };
        _ray.Exclude = exclude;
        _shapeQuery.Exclude = exclude;
    }

    public readonly record struct DebugSegment(Vector3 A, Vector3 B, Color Color);

    public int QueriesThisTick { get; private set; }

    public int PeakQueries { get; private set; }

    public bool RecordDebug { get; set; }

    public IReadOnlyList<DebugSegment> DebugSegments => _debug;

    public FrontProbe LastFront { get; private set; }

    public void BeginTick()
    {
        QueriesThisTick = 0;
        _debug.Clear();
    }

    public void ResetPeak() => PeakQueries = 0;

    private PhysicsDirectSpaceState3D Space => _body.GetWorld3D().DirectSpaceState;

    // ------------------------------------------------------------------ primitives

    public ProbeHit Ray(Vector3 from, Vector3 to)
    {
        Count();
        _ray.From = from;
        _ray.To = to;
        _ray.CollisionMask = _body.CollisionMask;
        Dictionary hit = Space.IntersectRay(_ray);
        if (hit.Count == 0)
        {
            Record(from, to, new Color(0.5f, 0.5f, 0.5f));
            return ProbeHit.None;
        }

        var point = hit["position"].AsVector3();
        Record(from, point, Colors.Yellow);
        Record(point, point + hit["normal"].AsVector3() * 0.25f, Colors.Magenta);
        return new ProbeHit(true, point, hit["normal"].AsVector3(), hit["collider"].AsGodotObject());
    }

    /// <summary>True when a capsule standing on <paramref name="feet"/> overlaps nothing.</summary>
    public bool CapsuleFree(Vector3 feet, float height, float radius)
    {
        Count();
        const float lift = 0.04f;
        _clearanceCapsule.Radius = radius - 0.02f;
        _clearanceCapsule.Height = Mathf.Max(height - lift, _clearanceCapsule.Radius * 2f);
        _shapeQuery.Shape = _clearanceCapsule;
        _shapeQuery.Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * (lift + _clearanceCapsule.Height * 0.5f));
        _shapeQuery.Motion = Vector3.Zero;
        _shapeQuery.CollisionMask = _body.CollisionMask;
        bool free = Space.IntersectShape(_shapeQuery, 1).Count == 0;
        Color c = free ? Colors.LimeGreen : Colors.Red;
        Record(feet, feet + Vector3.Up * height, c);
        Record(feet + Vector3.Left * radius, feet + Vector3.Right * radius, c);
        return free;
    }

    // ------------------------------------------------------------------ high level

    /// <summary>
    /// Sweep a thin box (capsule width, from knee to above-head height) forward, then read the
    /// wall face, the ledge top above it, standability and the far edge. ~2–6 queries, and
    /// only 1 when nothing is in front.
    /// </summary>
    /// <param name="reach">Look-ahead distance measured from the capsule surface.</param>
    public FrontProbe ProbeFront(Vector3 feet, Vector3 dir, float reach, float radius, float maxLedgeHeight, float vaultMaxDepth)
    {
        float travel = radius + reach;
        LastFront = FrontProbe.None;
        const float bottom = 0.06f; // low enough to see curbs (step-up), slopes are rejected by the normal test
        float top = maxLedgeHeight + 0.15f;
        _castBox.Size = new Vector3(radius * 1.4f, top - bottom, 0.1f);
        Vector3 start = feet + Vector3.Up * ((bottom + top) * 0.5f);
        var basis = Basis.LookingAt(dir, Vector3.Up);
        _shapeQuery.Shape = _castBox;
        _shapeQuery.Transform = new Transform3D(basis, start);
        _shapeQuery.Motion = dir * travel;
        _shapeQuery.CollisionMask = _body.CollisionMask;

        Count();
        float[] fractions = Space.CastMotion(_shapeQuery);
        Record(start, start + dir * travel, new Color(0.3f, 0.6f, 1f));
        if (fractions.Length < 2 || fractions[1] >= 1f)
        {
            return FrontProbe.None;
        }

        float unsafeFraction = fractions[1];
        _shapeQuery.Transform = new Transform3D(basis, start + dir * (travel * unsafeFraction));
        _shapeQuery.Motion = Vector3.Zero;
        Count();
        Dictionary rest = Space.GetRestInfo(_shapeQuery);
        if (rest.Count == 0)
        {
            return FrontProbe.None;
        }

        Vector3 wallPoint = rest["point"].AsVector3();
        Vector3 rawNormal = rest["normal"].AsVector3();
        var n = new Vector3(rawNormal.X, 0f, rawNormal.Z);
        if (Mathf.Abs(rawNormal.Y) > 0.35f || n.LengthSquared() < 1e-4f)
        {
            return FrontProbe.None; // slope or floor lip, not a wall face
        }

        n = n.Normalized();
        if (n.Dot(-dir) < 0.5f)
        {
            return FrontProbe.None; // grazing contact, not facing the obstacle
        }

        Record(wallPoint, wallPoint + n * 0.4f, Colors.Orange);
        float wallDistance = Mathf.Max(0f, travel * unsafeFraction - radius);
        bool allowsWallRun = AllowsWallRun(rest);

        // Ledge top: cast down just behind the face, from above the grab height.
        Vector3 inset = new Vector3(wallPoint.X, 0f, wallPoint.Z) - n * 0.04f;
        ProbeHit topHit = Ray(new Vector3(inset.X, feet.Y + maxLedgeHeight + 0.25f, inset.Z), new Vector3(inset.X, feet.Y + 0.04f, inset.Z));
        if (!topHit.Valid || topHit.Normal.Y < 0.7f)
        {
            return LastFront = new FrontProbe(true, wallPoint, n, wallDistance, false, 0f, 0f, false, false, 0f, Vector3.Zero, allowsWallRun);
        }

        float topY = topHit.Point.Y;
        float height = topY - feet.Y;

        // Standable top: is there floor where the capsule would stand on top?
        Vector3 standXZ = inset - n * (radius * 2f + 0.1f);
        ProbeHit standHit = Ray(new Vector3(standXZ.X, topY + 0.5f, standXZ.Z), new Vector3(standXZ.X, topY - 0.3f, standXZ.Z));
        bool standable = standHit.Valid && standHit.Normal.Y > 0.7f && Mathf.Abs(standHit.Point.Y - topY) < 0.15f;

        bool hasFarEdge = false;
        float depth = 0f;
        Vector3 farEdge = Vector3.Zero;
        if (!standable)
        {
            // Far face: shoot back toward the runner from beyond the max vault depth, just under the top.
            float span = vaultMaxDepth + 0.35f;
            Vector3 from = new Vector3(inset.X, topY - 0.05f, inset.Z) - n * span;
            ProbeHit far = Ray(from, from + n * span);
            if (far.Valid)
            {
                hasFarEdge = true;
                farEdge = new Vector3(far.Point.X, topY, far.Point.Z);
                depth = new Vector2(far.Point.X - wallPoint.X, far.Point.Z - wallPoint.Z).Length();
            }
        }

        return LastFront = new FrontProbe(true, wallPoint, n, wallDistance, true, topY, height, standable, hasFarEdge, depth, farEdge, allowsWallRun);
    }

    /// <summary>Two stacked side rays; both must agree on a near-vertical wall (never trust a single ray).</summary>
    public ProbeHit ProbeSideWall(Vector3 feet, Vector3 sideDir, float reach)
    {
        ProbeHit low = Ray(feet + Vector3.Up * 0.9f, feet + Vector3.Up * 0.9f + sideDir * reach);
        if (!low.Valid || Mathf.Abs(low.Normal.Y) > 0.3f || !AllowsWallRun(low.Collider))
        {
            return ProbeHit.None;
        }

        ProbeHit high = Ray(feet + Vector3.Up * 1.5f, feet + Vector3.Up * 1.5f + sideDir * reach);
        if (!high.Valid || high.Normal.Dot(low.Normal) < 0.9f)
        {
            return ProbeHit.None;
        }

        var n = new Vector3(low.Normal.X, 0f, low.Normal.Z).Normalized();
        return low with { Normal = n };
    }

    private static bool AllowsWallRun(Dictionary rest) =>
        !rest.ContainsKey("collider_id") || AllowsWallRun(GodotObject.InstanceFromId(rest["collider_id"].AsUInt64()));

    /// <summary>Colliders tagged with metadata <c>wallrun = false</c> refuse wall runs.</summary>
    private static bool AllowsWallRun(GodotObject? collider) =>
        collider is not Node node || !node.HasMeta(WallRunMetaKey) || node.GetMeta(WallRunMetaKey).AsBool();

    private void Count()
    {
        QueriesThisTick++;
        if (QueriesThisTick > PeakQueries)
        {
            PeakQueries = QueriesThisTick;
        }
    }

    private void Record(Vector3 a, Vector3 b, Color c)
    {
        if (RecordDebug)
        {
            _debug.Add(new DebugSegment(a, b, c));
        }
    }
}
