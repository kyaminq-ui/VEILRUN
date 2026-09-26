using Godot;

namespace Veilrun.Core.Settings;

/// <summary>
/// Per-player comfort and control preferences. These are client-side only and never
/// affect simulation. Persistence to user:// is planned (see docs/KNOWN_ISSUES.md).
/// </summary>
[GlobalClass]
public partial class UserSettings : Resource
{
    /// <summary>Horizontal field of view in degrees.</summary>
    [ExportGroup("Camera")]
    [Export(PropertyHint.Range, "70,120,1")] public float FieldOfView { get; set; } = 95f;
    [Export] public bool DynamicFov { get; set; } = true;
    [Export(PropertyHint.Range, "0,1,0.05")] public float HeadBob { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float CameraRoll { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float LandingShake { get; set; } = 1f;
    /// <summary>Master comfort switch: clamps every procedural camera effect and disables dynamic FOV.</summary>
    [Export] public bool ReducedCameraMotion { get; set; }
    /// <summary>Stored for the settings menu. Godot 4.7 has no built-in motion blur; not implemented yet.</summary>
    [Export] public bool MotionBlur { get; set; }

    /// <summary>Degrees of rotation per mouse pixel.</summary>
    [ExportGroup("Controls")]
    [Export(PropertyHint.Range, "0.01,0.5,0.005")] public float MouseSensitivity { get; set; } = 0.08f;
    /// <summary>Degrees per second at full stick deflection.</summary>
    [Export(PropertyHint.Range, "30,600,5")] public float GamepadLookSpeed { get; set; } = 220f;
    [Export] public bool InvertY { get; set; }

    /// <summary>Scale applied to procedural camera effects when reduced motion is on.</summary>
    public const float ReducedMotionScale = 0.25f;

    public float EffectiveHeadBob => ReducedCameraMotion ? HeadBob * ReducedMotionScale : HeadBob;
    public float EffectiveCameraRoll => ReducedCameraMotion ? 0f : CameraRoll;
    public float EffectiveLandingShake => ReducedCameraMotion ? LandingShake * ReducedMotionScale : LandingShake;
    public bool EffectiveDynamicFov => DynamicFov && !ReducedCameraMotion;
}
