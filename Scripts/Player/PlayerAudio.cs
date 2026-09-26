using System.Text;
using Godot;
using Veilrun.Audio;
using Veilrun.Tools.Debug;

namespace Veilrun.Player;

/// <summary>
/// First-person movement audio. Purely presentational: it reacts to Runner/motor events
/// (footstep, jump, landing) and reads motor state (speed) — it never drives simulation.
/// Remote runners (M3) will use a positional variant fed by the same events.
/// </summary>
public partial class PlayerAudio : Node, IDebugInfoProvider
{
    private const int OneShotVoices = 6;
    private const string Bus = "Movement";

    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[OneShotVoices];
    private int _nextVoice;
    private AudioStreamPlayer? _breathCalm;
    private AudioStreamPlayer? _breathHeavy;
    private AudioStreamPlayer? _wind;
    private Runner? _runner;
    private DevTools? _dev;
    private float _exertion;
    private SurfaceType _lastSurface;

    [Export] public MovementAudioBank Bank { get; set; } = null!;

    public string DebugTitle => "Audio";

    public override void _Ready()
    {
        Bank ??= new MovementAudioBank();
        string bus = AudioServer.GetBusIndex(Bus) >= 0 ? Bus : "Master";
        for (int i = 0; i < OneShotVoices; i++)
        {
            _voices[i] = new AudioStreamPlayer { Name = $"Voice{i}", Bus = bus };
            AddChild(_voices[i]);
        }

        _breathCalm = CreateLoop("BreathCalm", Bank.BreathCalmLoop, bus);
        _breathHeavy = CreateLoop("BreathHeavy", Bank.BreathHeavyLoop, bus);
        _wind = CreateLoop("Wind", Bank.WindLoop, bus);
    }

    public void Bind(Runner runner)
    {
        _runner = runner;
        runner.Jumped += OnJumped;
        runner.Landed += OnLanded;
        runner.Footstep += OnFootstep;
        runner.Respawned += OnRespawned;
        _dev = DevTools.Find(this);
        _dev?.Register(this);
    }

    public override void _ExitTree()
    {
        if (_runner is not null)
        {
            _runner.Jumped -= OnJumped;
            _runner.Landed -= OnLanded;
            _runner.Footstep -= OnFootstep;
            _runner.Respawned -= OnRespawned;
        }

        _dev?.Unregister(this);
    }

    public override void _Process(double delta)
    {
        if (_runner is null)
        {
            return;
        }

        float dt = (float)delta;
        ref readonly MotorState s = ref _runner.Motor.State;
        MovementTuning move = _runner.Motor.Tuning;

        // Exertion: builds while sprinting, recovers when slow. Drives the breathing blend.
        float speed = s.HorizontalSpeed;
        float effort = Mathf.Clamp(Mathf.InverseLerp(move.WalkSpeed, move.SprintSpeed, speed), 0f, 1f);
        _exertion = effort > 0.5f
            ? _exertion + Bank.ExertionGainPerSecond * effort * dt
            : _exertion - Bank.ExertionRecoveryPerSecond * (1f - effort) * dt;
        _exertion = Mathf.Clamp(_exertion, 0f, 1f);

        SetLoopVolume(_breathCalm, Bank.BreathVolumeDb, 1f - _exertion);
        SetLoopVolume(_breathHeavy, Bank.BreathVolumeDb, _exertion);

        // Wind: rushing air from total speed (includes falling), pitched up with speed.
        float airSpeed = s.Velocity.Length();
        float wind = Mathf.Clamp(Mathf.InverseLerp(Bank.WindMinSpeed, Bank.WindMaxSpeed, airSpeed), 0f, 1f);
        SetLoopVolume(_wind, Bank.WindMaxVolumeDb, wind * wind);
        if (_wind is not null)
        {
            _wind.PitchScale = 0.85f + 0.4f * wind;
        }
    }

    public void AppendDebugInfo(StringBuilder sb)
    {
        sb.Append("surface ").Append(_lastSurface).Append("   exertion ").Append((_exertion * 100f).ToString("0")).AppendLine("%");
    }

    private void OnFootstep()
    {
        if (_runner is null)
        {
            return;
        }

        _lastSurface = ProbeSurface();
        MovementTuning move = _runner.Motor.Tuning;
        float t = Mathf.Clamp(Mathf.InverseLerp(move.WalkSpeed, move.SprintSpeed, _runner.Motor.State.HorizontalSpeed), 0f, 1f);
        Play(Bank.FootstepFor(_lastSurface), Mathf.Lerp(Bank.FootstepWalkAttenuationDb, 0f, t));
    }

    private void OnJumped()
    {
        Play(Bank.Jump, 0f);
        _exertion = Mathf.Min(1f, _exertion + Bank.ExertionPerJump);
    }

    private void OnLanded(LandingEvent landing)
    {
        Play(Bank.LandingFor(landing.Type), 0f);
        if (landing.Type != LandingType.Soft)
        {
            _exertion = Mathf.Min(1f, _exertion + Bank.ExertionPerHardLanding);
        }
    }

    private void OnRespawned() => _exertion = 0f;

    private void Play(SfxEvent sfx, float extraDb)
    {
        if (sfx.Stream is not { } stream)
        {
            return;
        }

        AudioStreamPlayer voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % OneShotVoices;
        voice.Stream = stream;
        voice.VolumeDb = sfx.VolumeDb + extraDb;
        voice.Play();
    }

    /// <summary>Only runs on footstep events (a few per second), never per frame.</summary>
    private SurfaceType ProbeSurface()
    {
        if (_runner is null)
        {
            return SurfaceType.Concrete;
        }

        Vector3 origin = _runner.GlobalPosition + Vector3.Up * 0.2f;
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + Vector3.Down * 0.6f, _runner.CollisionMask,
            new Godot.Collections.Array<Rid> { _runner.GetRid() });
        var hit = _runner.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 ? Surfaces.Resolve(hit["collider"].AsGodotObject()) : SurfaceType.Concrete;
    }

    private AudioStreamPlayer? CreateLoop(string name, AudioStream? stream, string bus)
    {
        if (stream is null)
        {
            return null;
        }

        EnsureLooping(stream);
        var player = new AudioStreamPlayer { Name = name, Stream = stream, Bus = bus, VolumeDb = -80f, Autoplay = true };
        AddChild(player);
        return player;
    }

    /// <summary>WAV loops normally come from the file's smpl chunk; force it if the import lost it.</summary>
    private static void EnsureLooping(AudioStream stream)
    {
        if (stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Disabled } wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
        }
    }

    private static void SetLoopVolume(AudioStreamPlayer? player, float fullDb, float linear)
    {
        if (player is not null)
        {
            player.VolumeDb = linear <= 0.001f ? -80f : fullDb + Mathf.LinearToDb(linear);
        }
    }
}
