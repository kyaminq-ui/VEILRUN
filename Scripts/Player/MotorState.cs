using Godot;

namespace Veilrun.Player;

public enum LocomotionMode : byte
{
    Idle,
    Walk,
    Run,
    Sprint,
    Airborne,
    Recovering,
}

public enum LandingType : byte
{
    Soft,
    Medium,
    Heavy,
    Deadly,
}

/// <summary>
/// Complete, copyable simulation state of the motor. Restoring a MotorState and replaying
/// the same InputCommands must reproduce the same result; this is the contract that
/// client-side prediction and server reconciliation (M3) are built on.
/// </summary>
public struct MotorState
{
    public uint Tick;
    public Vector3 Position;
    public Vector3 Velocity;
    public bool IsGrounded;
    public Vector3 GroundNormal;
    public float TimeSinceGrounded;
    public float AirTime;
    public bool HasJumpedSinceGrounded;
    public float JumpBufferRemaining;
    public float RecoveryRemaining;
    /// <summary>Move-speed multiplier applied while RecoveryRemaining &gt; 0 (depends on landing tier).</summary>
    public float RecoveryMoveMultiplier;
    /// <summary>Stride phase in [0, 2): one footstep per integer crossing. Drives footsteps and head bob.</summary>
    public float StrideCycle;
    public InputButtons PreviousButtons;
    public LocomotionMode Mode;

    public readonly float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();
}

public readonly record struct LandingEvent(LandingType Type, float ImpactSpeed, float Intensity, Vector3 Position);

/// <summary>Things that happened during one motor tick. Consumed by camera, audio, telemetry.</summary>
public struct MotorEvents
{
    public bool Jumped;
    public bool LeftGround;
    public bool Footstep;
    public LandingEvent? Landed;
}
