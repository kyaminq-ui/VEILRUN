using Godot;

namespace Veilrun.Player;

/// <summary>
/// Every locomotion value lives here. Designers author intent-level numbers
/// (jump height in meters, time to apex, fall heights) and the physics constants are
/// derived, so changing "how high" never requires re-deriving gravity by hand.
/// Units: meters, seconds, meters/second. Metrics produced by these values are
/// measured by the automated tests and recorded in docs/PARKOUR_METRICS.md.
/// </summary>
[GlobalClass]
public partial class MovementTuning : Resource
{
    [ExportGroup("Body")]
    [Export(PropertyHint.Range, "0.2,0.6,0.01,suffix:m")] public float CapsuleRadius { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "1.0,2.2,0.01,suffix:m")] public float CapsuleHeight { get; set; } = 1.8f;
    [Export(PropertyHint.Range, "1.0,2.0,0.01,suffix:m")] public float EyeHeight { get; set; } = 1.65f;
    [Export(PropertyHint.Range, "0,89,1,suffix:°")] public float MaxFloorAngleDegrees { get; set; } = 46f;
    /// <summary>How far the motor snaps down to stay glued to ramps and small drops.</summary>
    [Export(PropertyHint.Range, "0,1,0.01,suffix:m")] public float FloorSnapLength { get; set; } = 0.35f;

    // Speed builds automatically while the player keeps pushing forward (no walk/sprint keys):
    //   0 ──WalkAcceleration──▶ WalkSpeed ──RunAcceleration──▶ RunSpeed ──SprintAcceleration──▶ SprintSpeed
    // The top speed available depends on the input direction (forward / strafe / backwards).
    [ExportGroup("Ground Speeds")]
    [Export(PropertyHint.Range, "0.5,5,0.1,suffix:m/s")] public float WalkSpeed { get; set; } = 2.2f;
    [Export(PropertyHint.Range, "2,10,0.1,suffix:m/s")] public float RunSpeed { get; set; } = 5.5f;
    [Export(PropertyHint.Range, "4,14,0.1,suffix:m/s")] public float SprintSpeed { get; set; } = 8.0f;
    /// <summary>Top speed when the input points sideways (strafing never reaches sprint).</summary>
    [Export(PropertyHint.Range, "1,10,0.1,suffix:m/s")] public float StrafeMaxSpeed { get; set; } = 4.5f;
    /// <summary>Top speed when the input points straight backwards.</summary>
    [Export(PropertyHint.Range, "0.5,8,0.1,suffix:m/s")] public float BackpedalMaxSpeed { get; set; } = 3.0f;
    /// <summary>Input direction must be at least this "forward" (dot with facing) to build past run speed.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float SprintMinForwardDot { get; set; } = 0.7f;
    /// <summary>Analog stick deflection below this caps speed at WalkSpeed (scaled). Keyboard is always full deflection.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float AnalogWalkThreshold { get; set; } = 0.6f;

    /// <summary>0 → walk speed. High = the first step is immediate.</summary>
    [ExportGroup("Ground Acceleration")]
    [Export(PropertyHint.Range, "5,100,1,suffix:m/s²")] public float WalkAcceleration { get; set; } = 30f;
    /// <summary>Walk → run speed.</summary>
    [Export(PropertyHint.Range, "1,60,0.5,suffix:m/s²")] public float RunAcceleration { get; set; } = 12f;
    /// <summary>Run → sprint speed. Low = top speed is earned by keeping a clean line.</summary>
    [Export(PropertyHint.Range, "0.5,30,0.1,suffix:m/s²")] public float SprintAcceleration { get; set; } = 2.2f;
    /// <summary>Braking when there is no input or the input points backwards.</summary>
    [Export(PropertyHint.Range, "5,100,1,suffix:m/s²")] public float GroundDeceleration { get; set; } = 22f;
    /// <summary>Speed shed while still pushing but above the current cap (e.g. turning into a strafe). Low = momentum carries.</summary>
    [Export(PropertyHint.Range, "0.5,50,0.5,suffix:m/s²")] public float OverspeedDeceleration { get; set; } = 8f;
    /// <summary>Heading change rate at walk/run speeds.</summary>
    [Export(PropertyHint.Range, "1,60,0.5,suffix:rad/s")] public float TurnRateLowSpeed { get; set; } = 20f;
    /// <summary>Heading change rate at full sprint. Lower = wider lines at speed.</summary>
    [Export(PropertyHint.Range, "1,60,0.5,suffix:rad/s")] public float TurnRateHighSpeed { get; set; } = 7f;
    /// <summary>Fraction of speed above run speed lost per radian turned. Rewards clean lines.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float TurnSpeedLossPerRadian { get; set; } = 0.3f;
    /// <summary>Wish/velocity dot below which input is treated as braking rather than turning.</summary>
    [Export(PropertyHint.Range, "-1,0,0.05")] public float ReverseDotThreshold { get; set; } = -0.5f;

    [ExportGroup("Air")]
    [Export(PropertyHint.Range, "0,50,0.5,suffix:m/s²")] public float AirAcceleration { get; set; } = 10f;
    /// <summary>Air control may steer freely up to this speed; above it, it can only redirect.</summary>
    [Export(PropertyHint.Range, "0,14,0.1,suffix:m/s")] public float AirControlMaxSpeed { get; set; } = 2.5f;

    [ExportGroup("Jump")]
    [Export(PropertyHint.Range, "0.3,3,0.01,suffix:m")] public float JumpHeight { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "0.15,1,0.01,suffix:s")] public float JumpTimeToApex { get; set; } = 0.36f;
    /// <summary>Gravity multiplier while descending. &gt;1 gives a snappier, weightier fall.</summary>
    [Export(PropertyHint.Range, "1,3,0.05")] public float FallGravityMultiplier { get; set; } = 1.4f;
    [Export(PropertyHint.Range, "10,80,1,suffix:m/s")] public float TerminalFallSpeed { get; set; } = 45f;
    [Export(PropertyHint.Range, "0,0.3,0.01,suffix:s")] public float CoyoteTime { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,0.3,0.01,suffix:s")] public float JumpBufferTime { get; set; } = 0.12f;

    // Landing tiers by fall height measured from the apex of the fall:
    //   Soft < MediumLandingHeight ≤ Medium < HeavyLandingHeight ≤ Heavy < DeadlyFallHeight ≤ Deadly
    /// <summary>Falls at or above this height (from apex) are Medium landings.</summary>
    [ExportGroup("Landing")]
    [Export(PropertyHint.Range, "1,10,0.01,suffix:m")] public float MediumLandingHeight { get; set; } = 3.20f;
    /// <summary>Falls at or above this height are Heavy landings.</summary>
    [Export(PropertyHint.Range, "1,15,0.01,suffix:m")] public float HeavyLandingHeight { get; set; } = 5.12f;
    /// <summary>Falls at or above this height are Deadly.</summary>
    [Export(PropertyHint.Range, "2,30,0.01,suffix:m")] public float DeadlyFallHeight { get; set; } = 6.45f;

    [ExportSubgroup("Medium")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float MediumSpeedRetention { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")] public float MediumRecoveryTime { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float MediumMoveMultiplier { get; set; } = 0.8f;

    [ExportSubgroup("Heavy")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float HeavySpeedRetention { get; set; } = 0.3f;
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")] public float HeavyRecoveryTime { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float HeavyMoveMultiplier { get; set; } = 0.45f;

    /// <summary>Stride length at walk speed (one footstep).</summary>
    [ExportGroup("Stride")]
    [Export(PropertyHint.Range, "0.3,2,0.05,suffix:m")] public float StepLengthWalk { get; set; } = 0.75f;
    /// <summary>Stride length at sprint speed (one footstep). Drives footsteps and head bob.</summary>
    [Export(PropertyHint.Range, "0.5,3,0.05,suffix:m")] public float StepLengthSprint { get; set; } = 1.9f;

    // ---- Derived values (not exported; computed from the authoring values above) ----

    /// <summary>Upward gravity: g = 2h / t².</summary>
    public float RiseGravity => 2f * JumpHeight / (JumpTimeToApex * JumpTimeToApex);

    public float FallGravity => RiseGravity * FallGravityMultiplier;

    /// <summary>Initial jump velocity: v = 2h / t.</summary>
    public float JumpVelocity => 2f * JumpHeight / JumpTimeToApex;

    /// <summary>Impact speed after free-falling <paramref name="height"/> meters under fall gravity.</summary>
    public float ImpactSpeedForFall(float height) => Mathf.Sqrt(2f * FallGravity * Mathf.Max(0f, height));

    public float MediumLandingSpeed => ImpactSpeedForFall(MediumLandingHeight);

    public float HeavyLandingSpeed => ImpactSpeedForFall(HeavyLandingHeight);

    public float DeadlyLandingSpeed => ImpactSpeedForFall(DeadlyFallHeight);

    public float MaxFloorAngleRadians => Mathf.DegToRad(MaxFloorAngleDegrees);

    /// <summary>Step length for the current horizontal speed.</summary>
    public float StepLengthAt(float speed) =>
        Mathf.Lerp(StepLengthWalk, StepLengthSprint, Mathf.Clamp(Mathf.InverseLerp(WalkSpeed, SprintSpeed, speed), 0f, 1f));
}
