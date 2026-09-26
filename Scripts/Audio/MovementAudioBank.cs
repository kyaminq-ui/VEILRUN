using Godot;
using Veilrun.Player;

namespace Veilrun.Audio;

/// <summary>
/// All movement sounds of a runner plus their mix behaviour. Swapping placeholder audio
/// for final assets only means editing this resource (Resources/Audio/DefaultMovementAudio.tres).
/// </summary>
[GlobalClass]
public partial class MovementAudioBank : Resource
{
    [ExportGroup("Footsteps")]
    [Export] public SfxEvent FootstepConcrete { get; set; } = new();
    [Export] public SfxEvent FootstepMetal { get; set; } = new();
    /// <summary>Footstep gain at walk speed relative to sprint speed.</summary>
    [Export(PropertyHint.Range, "-30,0,0.5,suffix:dB")] public float FootstepWalkAttenuationDb { get; set; } = -9f;

    [ExportGroup("Jump & Landing")]
    [Export] public SfxEvent Jump { get; set; } = new();
    [Export] public SfxEvent LandSoft { get; set; } = new();
    [Export] public SfxEvent LandMedium { get; set; } = new();
    [Export] public SfxEvent LandHeavy { get; set; } = new();
    [Export] public SfxEvent LandDeadly { get; set; } = new();

    /// <summary>Hands on an obstacle: vault, mantle, ledge grab, ledge climb.</summary>
    [ExportGroup("Parkour")]
    [Export] public SfxEvent HandPlant { get; set; } = new();
    [Export] public SfxEvent Roll { get; set; } = new();
    [Export] public AudioStream? SlideLoop { get; set; }
    [Export(PropertyHint.Range, "-40,6,0.5,suffix:dB")] public float SlideVolumeDb { get; set; } = -4f;

    [ExportGroup("Breathing")]
    [Export] public AudioStream? BreathCalmLoop { get; set; }
    [Export] public AudioStream? BreathHeavyLoop { get; set; }
    [Export(PropertyHint.Range, "-40,6,0.5,suffix:dB")] public float BreathVolumeDb { get; set; } = -8f;
    /// <summary>Exertion gained per second at full sprint (0..1 scale).</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float ExertionGainPerSecond { get; set; } = 0.12f;
    /// <summary>Exertion recovered per second when at or below walk speed.</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float ExertionRecoveryPerSecond { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ExertionPerJump { get; set; } = 0.03f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ExertionPerHardLanding { get; set; } = 0.15f;

    [ExportGroup("Wind")]
    [Export] public AudioStream? WindLoop { get; set; }
    [Export(PropertyHint.Range, "0,20,0.1,suffix:m/s")] public float WindMinSpeed { get; set; } = 4f;
    [Export(PropertyHint.Range, "1,60,0.5,suffix:m/s")] public float WindMaxSpeed { get; set; } = 20f;
    [Export(PropertyHint.Range, "-40,6,0.5,suffix:dB")] public float WindMaxVolumeDb { get; set; } = -6f;

    public SfxEvent FootstepFor(SurfaceType surface) => surface switch
    {
        SurfaceType.Metal or SurfaceType.MetalGrate when !FootstepMetal.IsEmpty => FootstepMetal,
        _ => FootstepConcrete,
    };

    public SfxEvent LandingFor(LandingType type) => type switch
    {
        LandingType.Medium => LandMedium,
        LandingType.Heavy => LandHeavy,
        LandingType.Deadly => LandDeadly,
        _ => LandSoft,
    };
}
