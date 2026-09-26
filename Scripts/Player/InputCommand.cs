using System;
using Godot;

namespace Veilrun.Player;

[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Jump = 1 << 0,
    Crouch = 1 << 1,
}

/// <summary>
/// One tick of player intent. This is the unit sent client → server (M3) and replayed
/// during reconciliation, so it must contain everything the motor needs and nothing else.
/// Buttons are "held or pressed since last sample" so sub-tick taps are never lost;
/// edges are derived by the motor from the previous command.
/// </summary>
public readonly record struct InputCommand(
    uint Sequence,
    uint Tick,
    Vector2 Move,
    float Yaw,
    float Pitch,
    InputButtons Buttons)
{
    public bool Has(InputButtons button) => (Buttons & button) != 0;

    /// <summary>Move vector clamped to unit length. X = right, Y = forward.</summary>
    public Vector2 ClampedMove => Move.LengthSquared() > 1f ? Move.Normalized() : Move;
}
