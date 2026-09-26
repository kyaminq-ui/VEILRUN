using Godot;
using Veilrun.Traversal;

namespace Veilrun.Player;

public enum LocomotionMode : byte
{
    Idle,
    Walk,
    Run,
    Sprint,
    Airborne,
    Recovering,
    Crouch,
    /// <summary>A parkour move is active (see MotorState.Traversal).</summary>
    Traversal,
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

    // ---- Crouch & buffers ----
    public bool IsCrouched;
    /// <summary>Time left in which a crouch press still counts (landing roll, land-into-slide).</summary>
    public float CrouchBufferRemaining;

    // ---- Active traversal move (None = regular ground/air locomotion) ----
    public TraversalKind Traversal;
    public float TraversalTime;
    /// <summary>Scripted moves: path start / end (feet positions).</summary>
    public Vector3 TravStart;
    public Vector3 TravEnd;
    /// <summary>Wall or ledge face normal, pointing out of the wall toward the runner.</summary>
    public Vector3 TravNormal;
    /// <summary>Horizontal travel direction (vault/mantle/roll/slide heading, wall-run tangent).</summary>
    public Vector3 TravDir;
    /// <summary>Scripted moves: duration of the rising phase and of the horizontal phase.</summary>
    public float TravRiseTime;
    public float TravMoveTime;
    /// <summary>Vault apex feet height / ledge top height.</summary>
    public float TravPeakY;
    /// <summary>Fraction of the horizontal path covered during the rise.</summary>
    public float TravSplit;
    /// <summary>Speed carried by the move (exit speed for scripted moves, current speed for slide/roll/wall run).</summary>
    public float TravSpeed;
    /// <summary>Wall run: -1 wall on the left, +1 wall on the right.</summary>
    public sbyte WallSide;

    // ---- Cooldowns ----
    public float SlideCooldown;
    public float LedgeCooldown;
    public float WallCooldown;
    public Vector3 LastWallNormal;

    public readonly float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();
}

/// <param name="Rolled">True when a landing roll absorbed the impact (no Medium/Heavy penalty).</param>
public readonly record struct LandingEvent(LandingType Type, float ImpactSpeed, float Intensity, Vector3 Position, bool Rolled = false);

/// <summary>Things that happened during one motor tick. Consumed by camera, audio, telemetry.</summary>
public struct MotorEvents
{
    public bool Jumped;
    public bool LeftGround;
    public bool Footstep;
    public bool WallJumped;
    /// <summary>Wall kick off a vertical climb: presentation should turn the view around.</summary>
    public bool TurnAround;
    public TraversalKind Started;
    public TraversalKind Ended;
    public LandingEvent? Landed;
}
