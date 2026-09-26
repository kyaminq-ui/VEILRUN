using System.Collections.Generic;
using Godot;

namespace Veilrun.Tools.Debug;

/// <summary>
/// Immediate-mode 3D debug lines drawn on top of the world (no depth test).
/// Lines with duration 0 live for one rendered frame; physics-rate callers should pass
/// a duration of about one tick so lines don't flicker at high framerates.
/// Every call is a cheap no-op when disabled, so gameplay code can call freely.
/// </summary>
public partial class DebugDraw : Node3D
{
    private readonly List<DebugLine> _lines = new(512);
    private ImmediateMesh _mesh = null!;
    private StandardMaterial3D _material = null!;

    private readonly record struct DebugLine(Vector3 A, Vector3 B, Color Color, double ExpireAt);

    public bool Enabled { get; set; }

    public override void _Ready()
    {
        ProcessPriority = 1000; // draw after gameplay has submitted this frame's lines
        _mesh = new ImmediateMesh();
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = true,
        };
        AddChild(new MeshInstance3D
        {
            Name = "Lines",
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    public void Line(Vector3 a, Vector3 b, Color color, float duration = 0f)
    {
        if (Enabled)
        {
            _lines.Add(new DebugLine(a, b, color, Now() + duration));
        }
    }

    public void Arrow(Vector3 from, Vector3 vector, Color color, float duration = 0f)
    {
        if (!Enabled || vector.LengthSquared() < 1e-6f)
        {
            return;
        }

        Vector3 to = from + vector;
        Line(from, to, color, duration);
        Vector3 dir = vector.Normalized();
        Vector3 side = dir.Cross(Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up).Normalized();
        float head = Mathf.Min(0.2f, vector.Length() * 0.25f);
        Line(to, to - dir * head + side * head * 0.5f, color, duration);
        Line(to, to - dir * head - side * head * 0.5f, color, duration);
    }

    public void Cross(Vector3 at, float size, Color color, float duration = 0f)
    {
        if (!Enabled)
        {
            return;
        }

        float h = size * 0.5f;
        Line(at - new Vector3(h, 0, 0), at + new Vector3(h, 0, 0), color, duration);
        Line(at - new Vector3(0, h, 0), at + new Vector3(0, h, 0), color, duration);
        Line(at - new Vector3(0, 0, h), at + new Vector3(0, 0, h), color, duration);
    }

    /// <summary>Wireframe capsule standing on <paramref name="feet"/>.</summary>
    public void Capsule(Vector3 feet, float radius, float height, Color color, float duration = 0f)
    {
        if (!Enabled)
        {
            return;
        }

        const int segments = 12;
        for (int r = 0; r < 2; r++)
        {
            Vector3 center = feet + Vector3.Up * (r == 0 ? radius : height - radius);
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Tau * i / segments;
                float a1 = Mathf.Tau * (i + 1) / segments;
                Line(center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius,
                     center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius, color, duration);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.Tau * i / 4;
            Vector3 offset = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            Line(feet + Vector3.Up * radius + offset, feet + Vector3.Up * (height - radius) + offset, color, duration);
        }
    }

    public override void _Process(double delta)
    {
        _mesh.ClearSurfaces();
        if (_lines.Count == 0)
        {
            return;
        }

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _material);
        foreach (DebugLine line in _lines)
        {
            _mesh.SurfaceSetColor(line.Color);
            _mesh.SurfaceAddVertex(line.A);
            _mesh.SurfaceSetColor(line.Color);
            _mesh.SurfaceAddVertex(line.B);
        }

        _mesh.SurfaceEnd();

        double now = Now();
        _lines.RemoveAll(l => l.ExpireAt <= now);
    }

    public void Clear() => _lines.Clear();

    private static double Now() => Time.GetTicksUsec() / 1_000_000.0;
}
