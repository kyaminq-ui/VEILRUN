using Godot;

namespace Veilrun.Player;

/// <summary>
/// Designer-side amplitudes for procedural first-person camera effects.
/// Player comfort scaling on top of these lives in UserSettings.
/// </summary>
[GlobalClass]
public partial class CameraTuning : Resource
{
    /// <summary>Bob follows the motor's stride (MovementTuning.StepLength*), so it stays in sync with footsteps.</summary>
    [ExportGroup("Head Bob")]
    [Export(PropertyHint.Range, "0,0.15,0.001,suffix:m")] public float BobVerticalAmplitude { get; set; } = 0.03f;
    [Export(PropertyHint.Range, "0,0.15,0.001,suffix:m")] public float BobLateralAmplitude { get; set; } = 0.015f;
    /// <summary>How fast bob amplitude fades in/out (airborne, stopping).</summary>
    [Export(PropertyHint.Range, "1,30,0.5")] public float BobBlendSpeed { get; set; } = 10f;

    /// <summary>Downward camera velocity injected per m/s of impact speed.</summary>
    [ExportGroup("Landing")]
    [Export(PropertyHint.Range, "0,0.3,0.005")] public float LandingImpulsePerImpactSpeed { get; set; } = 0.09f;
    [Export(PropertyHint.Range, "10,600,5")] public float LandingSpringStiffness { get; set; } = 140f;
    /// <summary>1 = critically damped (no overshoot).</summary>
    [Export(PropertyHint.Range, "0.2,2,0.05")] public float LandingSpringDampingRatio { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0,0.6,0.01,suffix:m")] public float LandingMaxDip { get; set; } = 0.28f;
    /// <summary>Pitch kick at maximum dip.</summary>
    [Export(PropertyHint.Range, "0,10,0.1,suffix:°")] public float LandingPitchKickDegrees { get; set; } = 3.5f;

    [ExportGroup("Roll")]
    [Export(PropertyHint.Range, "0,6,0.1,suffix:°")] public float StrafeRollDegrees { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "1,30,0.5")] public float RollSmoothing { get; set; } = 8f;

    /// <summary>Extra FOV added at full sprint speed (dynamic FOV).</summary>
    [ExportGroup("Field Of View")]
    [Export(PropertyHint.Range, "0,20,0.5,suffix:°")] public float SprintFovBonusDegrees { get; set; } = 7f;
    [Export(PropertyHint.Range, "0.5,20,0.5")] public float FovSmoothing { get; set; } = 4f;
}
