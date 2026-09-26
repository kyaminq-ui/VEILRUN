using Godot;
using Veilrun.Core;
using Veilrun.Core.Settings;

namespace Veilrun.Player;

/// <summary>
/// Procedural first-person camera. It is a top-level node, never parented to an animated
/// head: every frame it rebuilds its transform from (a) the player's position interpolated
/// between the last two simulation ticks, (b) the latest look input, and (c) layered
/// comfort-scaled offsets (bob, landing dip, roll, dynamic FOV). None of these effects
/// change the aim direction except the small landing pitch kick.
/// </summary>
public partial class PlayerCamera : Node3D
{
    private const float MaxFrameDelta = 1f / 20f;

    private Runner? _runner;
    private PlayerInput? _input;

    private float _bobWeight;
    private float _dipOffset;
    private float _dipVelocity;
    private float _roll;
    private float _fovBonus;

    [Export] public CameraTuning Tuning { get; set; } = null!;
    [Export] public UserSettings Settings { get; set; } = null!;
    [Export] public Camera3D Camera { get; set; } = null!;

    public override void _Ready()
    {
        Tuning ??= new CameraTuning();
        Settings ??= new UserSettings();
        TopLevel = true;
        Camera.KeepAspect = Camera3D.KeepAspectEnum.Width;
        Camera.Fov = Settings.FieldOfView;
    }

    public void Bind(Runner runner, PlayerInput input)
    {
        _runner = runner;
        _input = input;
        ResetEffects();
    }

    public void ResetEffects()
    {
        _bobWeight = _dipOffset = _dipVelocity = _roll = _fovBonus = 0f;
    }

    public void OnLanded(in LandingEvent landing)
    {
        _dipVelocity -= landing.ImpactSpeed * Tuning.LandingImpulsePerImpactSpeed * Settings.EffectiveLandingShake;
    }

    public override void _Process(double delta)
    {
        if (_runner is null || _input is null)
        {
            return;
        }

        float dt = Mathf.Min((float)delta, MaxFrameDelta);
        ref readonly MotorState state = ref _runner.Motor.State;
        MovementTuning move = _runner.Motor.Tuning;
        float speed = state.HorizontalSpeed;
        float yaw = _input.Yaw;
        var yawBasis = new Basis(Vector3.Up, yaw);

        UpdateBobWeight(state, speed, move, dt);
        float fraction = (float)Engine.GetPhysicsInterpolationFraction();
        float bobPhase = InterpolatedStridePhase(fraction);
        UpdateLandingSpring(dt);
        UpdateRoll(state, yawBasis, move, dt);
        UpdateFov(speed, move, dt);

        float bobAmount = _bobWeight * Settings.EffectiveHeadBob;
        // Lowest point exactly on each footstep (phase = kπ); lateral sway alternates per foot.
        var bob = new Vector3(
            Mathf.Cos(bobPhase) * Tuning.BobLateralAmplitude * bobAmount,
            -(Mathf.Cos(2f * bobPhase) + 1f) * 0.5f * Tuning.BobVerticalAmplitude * bobAmount,
            0f);

        Vector3 feet = _runner.PreviousTickPosition.Lerp(_runner.CurrentTickPosition, fraction);
        Vector3 eye = feet + Vector3.Up * (move.EyeHeight + _dipOffset) + yawBasis * bob;
        GlobalTransform = new Transform3D(yawBasis, eye);

        float dipNormalized = Tuning.LandingMaxDip > 0f ? _dipOffset / Tuning.LandingMaxDip : 0f;
        float pitchKick = Mathf.DegToRad(Tuning.LandingPitchKickDegrees) * dipNormalized;
        Camera.Transform = new Transform3D(Basis.FromEuler(new Vector3(_input.Pitch + pitchKick, 0f, _roll)), Vector3.Zero);
        Camera.Fov = Settings.FieldOfView + _fovBonus;
    }

    private void UpdateBobWeight(in MotorState state, float speed, MovementTuning move, float dt)
    {
        float target = state.IsGrounded ? Mathf.Clamp(speed / move.SprintSpeed, 0f, 1f) : 0f;
        _bobWeight = MathUtil.ExpDecay(_bobWeight, target, Tuning.BobBlendSpeed, dt);
    }

    /// <summary>Stride cycle ([0,2) per two footsteps, owned by the motor) interpolated between ticks, as radians.</summary>
    private float InterpolatedStridePhase(float fraction)
    {
        float previous = _runner!.PreviousStrideCycle;
        float current = _runner.Motor.State.StrideCycle;
        if (current < previous)
        {
            current += 2f; // wrapped this tick
        }

        return Mathf.Lerp(previous, current, fraction) * Mathf.Pi;
    }

    private void UpdateLandingSpring(float dt)
    {
        float k = Tuning.LandingSpringStiffness;
        float c = 2f * Tuning.LandingSpringDampingRatio * Mathf.Sqrt(k);
        _dipVelocity += (-k * _dipOffset - c * _dipVelocity) * dt;
        _dipOffset += _dipVelocity * dt;
        _dipOffset = Mathf.Clamp(_dipOffset, -Tuning.LandingMaxDip, Tuning.LandingMaxDip * 0.5f);
    }

    private void UpdateRoll(in MotorState state, Basis yawBasis, MovementTuning move, float dt)
    {
        float lateral = state.Velocity.Dot(yawBasis.X);
        float target = -Mathf.Clamp(lateral / move.RunSpeed, -1f, 1f)
                       * Mathf.DegToRad(Tuning.StrafeRollDegrees) * Settings.EffectiveCameraRoll;
        _roll = MathUtil.ExpDecay(_roll, target, Tuning.RollSmoothing, dt);
    }

    private void UpdateFov(float speed, MovementTuning move, float dt)
    {
        float target = Settings.EffectiveDynamicFov
            ? Mathf.Clamp(Mathf.InverseLerp(move.RunSpeed, move.SprintSpeed, speed), 0f, 1f) * Tuning.SprintFovBonusDegrees
            : 0f;
        _fovBonus = MathUtil.ExpDecay(_fovBonus, target, Tuning.FovSmoothing, dt);
    }
}
