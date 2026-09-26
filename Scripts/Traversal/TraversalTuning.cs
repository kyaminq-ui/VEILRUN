using Godot;

namespace Veilrun.Traversal;

/// <summary>
/// Every parkour-move value (slide, vault, mantle, ledge, wall run, wall climb, roll).
/// Heights are measured from the runner's feet; distances from the capsule surface
/// unless noted. Measured results live in docs/PARKOUR_METRICS.md.
/// </summary>
[GlobalClass]
public partial class TraversalTuning : Resource
{
    [ExportGroup("Crouch")]
    [Export(PropertyHint.Range, "0.7,1.6,0.01,suffix:m")] public float CrouchCapsuleHeight { get; set; } = 1.1f;
    [Export(PropertyHint.Range, "0.5,1.5,0.01,suffix:m")] public float CrouchEyeHeight { get; set; } = 0.95f;
    [Export(PropertyHint.Range, "0.5,4,0.1,suffix:m/s")] public float CrouchSpeed { get; set; } = 2.0f;

    /// <summary>Also used by the landing roll (same low profile).</summary>
    [ExportGroup("Slide")]
    [Export(PropertyHint.Range, "0.7,1.4,0.01,suffix:m")] public float SlideCapsuleHeight { get; set; } = 0.9f;
    [Export(PropertyHint.Range, "0.3,1.2,0.01,suffix:m")] public float SlideEyeHeight { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "2,10,0.1,suffix:m/s")] public float SlideMinSpeed { get; set; } = 5.0f;
    [Export(PropertyHint.Range, "0,4,0.1,suffix:m/s")] public float SlideBoost { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "4,16,0.1,suffix:m/s")] public float SlideMaxSpeed { get; set; } = 9.5f;
    [Export(PropertyHint.Range, "0,30,0.5,suffix:m/s²")] public float SlideFriction { get; set; } = 6.0f;
    /// <summary>Fraction of gravity along the slope converted into slide acceleration.</summary>
    [Export(PropertyHint.Range, "0,1.5,0.05")] public float SlideSlopeGravityScale { get; set; } = 0.9f;
    [Export(PropertyHint.Range, "0.5,6,0.1,suffix:m/s")] public float SlideExitSpeed { get; set; } = 2.5f;
    [Export(PropertyHint.Range, "0,10,0.1,suffix:rad/s")] public float SlideTurnRate { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")] public float SlideCooldown { get; set; } = 0.3f;

    /// <summary>Probe reach in front of the capsule at rest; grows with speed (look-ahead).</summary>
    [ExportGroup("Obstacle Probe")]
    [Export(PropertyHint.Range, "0.1,2,0.05,suffix:m")] public float FrontReachBase { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0,0.3,0.01,suffix:s")] public float FrontReachPerSpeed { get; set; } = 0.08f;
    /// <summary>Minimum forward input (0..1) to trigger vault / mantle / ledge moves.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float MinForwardInput { get; set; } = 0.5f;

    [ExportGroup("Vault")]
    [Export(PropertyHint.Range, "0.1,1,0.01,suffix:m")] public float VaultMinHeight { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0.5,1.8,0.01,suffix:m")] public float VaultMaxHeight { get; set; } = 1.3f;
    /// <summary>Obstacles thicker than this are climbed onto (mantle) instead of vaulted.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05,suffix:m")] public float VaultMaxDepth { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "0,8,0.1,suffix:m/s")] public float VaultMinSpeed { get; set; } = 1.0f;
    /// <summary>Feet clearance above the obstacle top while passing over.</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01,suffix:m")] public float VaultClearance { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float VaultSpeedRetention { get; set; } = 0.95f;
    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")] public float VaultMinExitSpeed { get; set; } = 4.5f;
    /// <summary>Minimum time spent rising per meter of obstacle height (keeps the camera readable).</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01,suffix:s/m")] public float VaultRiseTimePerMeter { get; set; } = 0.14f;

    [ExportGroup("Mantle")]
    [Export(PropertyHint.Range, "0.1,1,0.01,suffix:m")] public float MantleMinHeight { get; set; } = 0.1f;
    /// <summary>Highest ledge climbed directly from the ground.</summary>
    [Export(PropertyHint.Range, "0.5,3,0.01,suffix:m")] public float MantleMaxHeight { get; set; } = 2.0f;
    /// <summary>While airborne, ledges up to this height above the feet are mantled directly; higher ones are grabbed.</summary>
    [Export(PropertyHint.Range, "0.5,3,0.01,suffix:m")] public float AirMantleMaxHeight { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,1,0.01,suffix:s/m")] public float MantleRiseTimePerMeter { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "0,0.5,0.01,suffix:s")] public float MantleRiseTimeBase { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0.05,0.6,0.01,suffix:s")] public float MantleForwardTime { get; set; } = 0.16f;
    /// <summary>Thick obstacles up to this height are a "quick climb" that keeps most of the momentum.</summary>
    [Export(PropertyHint.Range, "0.2,2,0.01,suffix:m")] public float QuickClimbMaxHeight { get; set; } = 1.05f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float QuickClimbSpeedRetention { get; set; } = 0.85f;
    /// <summary>Thick obstacles up to this height (curbs, steps) are stepped onto with no speed loss.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01,suffix:m")] public float StepUpMaxHeight { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,8,0.1,suffix:m/s")] public float MantleExitSpeed { get; set; } = 2.5f;

    /// <summary>Highest ledge (above the feet) the hands can catch while airborne.</summary>
    [ExportGroup("Ledge")]
    [Export(PropertyHint.Range, "1,3.5,0.01,suffix:m")] public float LedgeGrabMaxHeight { get; set; } = 2.35f;
    /// <summary>Feet hang this far below the ledge top.</summary>
    [Export(PropertyHint.Range, "1,2.5,0.01,suffix:m")] public float LedgeHangDrop { get; set; } = 1.95f;
    [Export(PropertyHint.Range, "0.02,0.5,0.01,suffix:s")] public float LedgeSnapTime { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "0.2,1.5,0.01,suffix:s")] public float LedgeClimbTime { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0,4,0.1,suffix:m/s")] public float LedgeDropPush { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")] public float LedgeRegrabCooldown { get; set; } = 0.4f;
    [Export(PropertyHint.Range, "0,4,0.05,suffix:m/s")] public float ShimmySpeed { get; set; } = 1.3f;

    [ExportGroup("Wall Run")]
    [Export(PropertyHint.Range, "1,12,0.1,suffix:m/s")] public float WallRunMinSpeed { get; set; } = 5.0f;
    [Export(PropertyHint.Range, "0.1,1.5,0.01,suffix:m")] public float WallRunReach { get; set; } = 0.45f;
    /// <summary>Largest angle between the velocity and the wall that still starts a wall run.</summary>
    [Export(PropertyHint.Range, "5,80,1,suffix:°")] public float WallRunMaxEntryAngle { get; set; } = 40f;
    [Export(PropertyHint.Range, "-15,5,0.1,suffix:m/s")] public float WallRunMinEntryVerticalSpeed { get; set; } = -4f;
    [Export(PropertyHint.Range, "0,8,0.1,suffix:m/s")] public float WallRunEntryUpSpeed { get; set; } = 3.0f;
    [Export(PropertyHint.Range, "0,30,0.5,suffix:m/s²")] public float WallRunGravity { get; set; } = 7f;
    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s²")] public float WallRunSpeedDecay { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0.2,4,0.05,suffix:s")] public float WallRunMaxTime { get; set; } = 1.3f;
    [Export(PropertyHint.Range, "0,4,0.1,suffix:m/s")] public float WallStickSpeed { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,3,0.05,suffix:s")] public float SameWallCooldown { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0,12,0.1,suffix:m/s")] public float WallJumpOutSpeed { get; set; } = 5.0f;
    [Export(PropertyHint.Range, "0,12,0.1,suffix:m/s")] public float WallJumpUpSpeed { get; set; } = 6.2f;
    [Export(PropertyHint.Range, "0,1.2,0.01")] public float WallJumpSpeedRetention { get; set; } = 0.95f;

    [ExportGroup("Wall Climb")]
    [Export(PropertyHint.Range, "0.1,2,0.05,suffix:m")] public float WallClimbReach { get; set; } = 0.6f;
    /// <summary>Minimum speed toward the wall to run up it.</summary>
    [Export(PropertyHint.Range, "0,8,0.1,suffix:m/s")] public float WallClimbMinSpeed { get; set; } = 2.5f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float WallClimbMinFacingDot { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "1,12,0.1,suffix:m/s")] public float WallClimbSpeed { get; set; } = 6.5f;
    [Export(PropertyHint.Range, "0.1,1.5,0.01,suffix:s")] public float WallClimbTime { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,12,0.1,suffix:m/s")] public float WallKickOutSpeed { get; set; } = 5.0f;
    [Export(PropertyHint.Range, "0,12,0.1,suffix:m/s")] public float WallKickUpSpeed { get; set; } = 5.5f;
    [Export(PropertyHint.Range, "0.05,1,0.01,suffix:s")] public float WallKickTurnTime { get; set; } = 0.22f;

    /// <summary>Crouch pressed this long before touching down converts a Medium/Heavy landing into a roll.</summary>
    [ExportGroup("Landing Roll")]
    [Export(PropertyHint.Range, "0,1,0.01,suffix:s")] public float RollWindow { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0.2,1.5,0.01,suffix:s")] public float RollDuration { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RollSpeedRetention { get; set; } = 0.9f;
    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")] public float RollMinSpeed { get; set; } = 4.0f;
    [Export(PropertyHint.Range, "0,10,0.1,suffix:rad/s")] public float RollTurnRate { get; set; } = 3.0f;

    /// <summary>Soft cap of physics queries per tick for one runner (reported in the Dev HUD, asserted in tests).</summary>
    [ExportGroup("Budget")]
    [Export(PropertyHint.Range, "1,32,1")] public int MaxQueriesPerTick { get; set; } = 8;
}
