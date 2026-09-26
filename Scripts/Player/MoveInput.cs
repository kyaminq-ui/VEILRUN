using Godot;

namespace Veilrun.Player;

/// <summary>
/// Per-tick interpretation of an <see cref="InputCommand"/> in world space, shared by
/// locomotion and every traversal move. Directions are horizontal (Y = 0) unit vectors.
/// </summary>
public readonly struct MoveInput
{
    public MoveInput(in InputCommand cmd, InputButtons previous)
    {
        Move = cmd.ClampedMove;
        Strength = Move.Length();
        Facing = new Vector3(-Mathf.Sin(cmd.Yaw), 0f, -Mathf.Cos(cmd.Yaw));
        Right = new Vector3(Mathf.Cos(cmd.Yaw), 0f, -Mathf.Sin(cmd.Yaw));
        Vector3 wish = Right * Move.X + Facing * Move.Y;
        WishDir = wish.LengthSquared() > 1e-6f ? wish.Normalized() : Vector3.Zero;
        Held = cmd.Buttons;
        Pressed = cmd.Buttons & ~previous;
    }

    /// <summary>Stick/keys, X = right, Y = forward, length ≤ 1.</summary>
    public Vector2 Move { get; }

    public float Strength { get; }

    public Vector3 Facing { get; }

    public Vector3 Right { get; }

    public Vector3 WishDir { get; }

    public InputButtons Held { get; }

    public InputButtons Pressed { get; }

    public float Forward => Move.Y;

    public bool IsHeld(InputButtons b) => (Held & b) != 0;

    public bool WasPressed(InputButtons b) => (Pressed & b) != 0;

    public static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    public static Vector3 Lift(Vector2 v, float y = 0f) => new(v.X, y, v.Y);
}
