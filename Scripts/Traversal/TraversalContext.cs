using Godot;
using Veilrun.Player;

namespace Veilrun.Traversal;

/// <summary>
/// Coordinates parkour moves for one motor: decides which move may start this tick,
/// dispatches the active move, and shares helpers (front probe cache, scripted paths,
/// begin/end bookkeeping). Holds NO gameplay state — everything lives in MotorState.
/// </summary>
public sealed class TraversalContext
{
    private bool _frontCached;
    private FrontProbe _front;

    internal TraversalContext(PlayerMotor motor)
    {
        Motor = motor;
    }

    internal PlayerMotor Motor { get; }

    internal MovementTuning Move => Motor.Tuning;

    internal TraversalTuning T => Motor.TraversalTuning;

    internal TraversalProbes Probes => Motor.Probes;

    internal float Dt { get; private set; }

    // ------------------------------------------------------------------ dispatch

    internal bool TryStart(ref MotorState s, in MoveInput input, float dt, ref MotorEvents e)
    {
        BeginTick(dt);
        if (!TryStartAny(ref s, input, ref e))
        {
            return false;
        }

        // Run the new move's first tick now so starting a move never costs a frozen tick.
        Tick(ref s, input, dt, ref e);
        return true;
    }

    private bool TryStartAny(ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        bool groundedish = s.IsGrounded || (s.TimeSinceGrounded <= Move.CoyoteTime && !s.HasJumpedSinceGrounded);
        if (groundedish)
        {
            // A buffered jump into a tall wall runs up it instead of jumping into it.
            if (s.JumpBufferRemaining > 0f && WallClimbMove.TryStart(this, ref s, input, ref e))
            {
                return true;
            }

            if (s.IsGrounded && (TryStartSlide(ref s, input, ref e, requirePress: true)
                                 || ObstacleMoves.TryStartFromGround(this, ref s, input, ref e)))
            {
                return true;
            }

            return false;
        }

        return ObstacleMoves.TryStartFromAir(this, ref s, input, ref e) || WallRunMove.TryStart(this, ref s, input, ref e);
    }

    internal void Tick(ref MotorState s, in MoveInput input, float dt, ref MotorEvents e)
    {
        BeginTick(dt);
        s.TraversalTime += dt;
        switch (s.Traversal)
        {
            case TraversalKind.Slide:
                SlideMove.Tick(this, ref s, input, ref e);
                break;
            case TraversalKind.Roll:
                RollMove.Tick(this, ref s, input, ref e);
                break;
            case TraversalKind.Vault:
            case TraversalKind.Mantle:
            case TraversalKind.LedgeClimb:
                ObstacleMoves.TickScripted(this, ref s, input, ref e);
                break;
            case TraversalKind.LedgeHang:
                LedgeHangMove.Tick(this, ref s, input, ref e);
                break;
            case TraversalKind.WallRun:
                WallRunMove.Tick(this, ref s, input, ref e);
                break;
            case TraversalKind.WallClimb:
                WallClimbMove.Tick(this, ref s, input, ref e);
                break;
            default:
                End(ref s, ref e);
                break;
        }
    }

    internal bool TryStartSlide(ref MotorState s, in MoveInput input, ref MotorEvents e, bool requirePress) =>
        SlideMove.TryStart(this, ref s, input, ref e, requirePress);

    internal void StartRoll(ref MotorState s, in MoveInput input, ref MotorEvents e) => RollMove.Start(this, ref s, input, ref e);

    // ------------------------------------------------------------------ shared helpers

    internal void Begin(ref MotorState s, TraversalKind kind, ref MotorEvents e)
    {
        s.Traversal = kind;
        s.TraversalTime = 0f;
        e.Started = kind;
    }

    internal void End(ref MotorState s, ref MotorEvents e)
    {
        e.Ended = s.Traversal;
        s.Traversal = TraversalKind.None;
        s.TraversalTime = 0f;
    }

    /// <summary>Look-ahead from the capsule surface, growing with speed.</summary>
    internal float FrontReach(in MotorState s) => T.FrontReachBase + T.FrontReachPerSpeed * s.HorizontalSpeed;

    /// <summary>Front probe along <paramref name="dir"/>, computed at most once per tick.</summary>
    internal FrontProbe ProbeFront(Vector3 dir, float reach)
    {
        if (!_frontCached)
        {
            _front = Probes.ProbeFront(Motor.BodyPosition, dir, reach, Move.CapsuleRadius, T.LedgeGrabMaxHeight, T.VaultMaxDepth);
            _frontCached = true;
        }

        return _front;
    }

    /// <summary>Invalidate the per-tick front cache (after the body moved within the same tick).</summary>
    internal void ForgetFront() => _frontCached = false;

    internal static float HorizontalDistanceToFace(Vector3 feet, in FrontProbe f)
    {
        Vector3 d = feet - f.WallPoint;
        return new Vector3(d.X, 0f, d.Z).Dot(f.WallNormal);
    }

    /// <summary>
    /// Scripted path used by vault / mantle / ledge climb / ledge snap:
    /// phase 1 (TravRiseTime) rises from start.Y to TravPeakY with an ease-out while covering
    /// TravSplit of the horizontal distance; phase 2 (TravMoveTime) covers the rest and blends
    /// the height from the peak to end.Y. Fully determined by MotorState → rollback-safe.
    /// </summary>
    internal static Vector3 EvaluatePath(in MotorState s, float time)
    {
        Vector3 start = s.TravStart;
        Vector3 end = s.TravEnd;
        var flat = new Vector3(end.X - start.X, 0f, end.Z - start.Z);
        float length = flat.Length();
        Vector3 dir = length > 1e-4f ? flat / length : Vector3.Zero;

        float along, y;
        if (time < s.TravRiseTime)
        {
            float u = time / s.TravRiseTime;
            along = s.TravSplit * length * u;
            y = Mathf.Lerp(start.Y, s.TravPeakY, Mathf.Sin(u * Mathf.Pi * 0.5f));
        }
        else
        {
            float u = s.TravMoveTime > 1e-5f ? Mathf.Clamp((time - s.TravRiseTime) / s.TravMoveTime, 0f, 1f) : 1f;
            along = s.TravSplit * length + (1f - s.TravSplit) * length * u;
            y = Mathf.Lerp(s.TravPeakY, end.Y, u);
        }

        return new Vector3(start.X, 0f, start.Z) + dir * along + Vector3.Up * y;
    }

    internal static float PathDuration(in MotorState s) => s.TravRiseTime + s.TravMoveTime;

    private void BeginTick(float dt)
    {
        Dt = dt;
        _frontCached = false;
    }
}
