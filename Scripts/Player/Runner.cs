using System;
using System.Text;
using Godot;
using Veilrun.Core;
using Veilrun.Tools.Debug;

namespace Veilrun.Player;

/// <summary>
/// Composition root of a player character ("Player" in the architecture doc).
/// Owns the fixed-tick loop: sample input → simulate motor → publish events.
/// Presentation components (camera, later animation/audio) only read state and events;
/// they never write simulation state. Future components (traversal, networking,
/// interaction, combat) plug in here rather than growing this class.
/// </summary>
public partial class Runner : CharacterBody3D, IDebugInfoProvider
{
    private uint _tick;
    private Transform3D _spawn;
    private DevTools? _dev;
    private LandingEvent? _lastLanding;

    [Export] public MovementTuning Tuning { get; set; } = null!;
    [Export] public PlayerInput PlayerInput { get; set; } = null!;
    [Export] public PlayerCamera PlayerCamera { get; set; } = null!;
    [Export] public CollisionShape3D CollisionShape { get; set; } = null!;
    /// <summary>Optional: remote/headless runners may have no audio.</summary>
    [Export] public PlayerAudio? PlayerAudio { get; set; }
    /// <summary>Below this height the runner is returned to spawn (test maps only).</summary>
    [Export] public float KillPlaneY { get; set; } = -50f;

    public PlayerMotor Motor { get; private set; } = null!;

    /// <summary>Feet position at the previous and current simulation tick (for render interpolation).</summary>
    public Vector3 PreviousTickPosition { get; private set; }

    public Vector3 CurrentTickPosition { get; private set; }

    /// <summary>Stride cycle at the previous tick (for render interpolation of head bob).</summary>
    public float PreviousStrideCycle { get; private set; }

    public string DebugTitle => $"{Name} (local)";

    public event Action? Jumped;
    public event Action? Footstep;
    public event Action<LandingEvent>? Landed;
    public event Action? Respawned;

    public override void _Ready()
    {
        Tuning ??= new MovementTuning();
        ApplyCollisionShape();
        Motor = new PlayerMotor(this, Tuning);
        _spawn = GlobalTransform;
        PreviousTickPosition = CurrentTickPosition = GlobalPosition;

        PlayerInput.SetLook(GlobalRotation.Y, 0f);
        PlayerCamera.Bind(this, PlayerInput);
        PlayerAudio?.Bind(this);

        _dev = DevTools.Find(this);
        _dev?.Register(this);
    }

    public override void _ExitTree() => _dev?.Unregister(this);

    public override void _PhysicsProcess(double delta)
    {
        _tick++;
        PreviousTickPosition = CurrentTickPosition;
        PreviousStrideCycle = Motor.State.StrideCycle;

        InputCommand cmd = PlayerInput.Sample(_tick);
        MotorEvents events = Motor.Simulate(cmd, (float)delta);
        CurrentTickPosition = GlobalPosition;

        if (events.Jumped)
        {
            Jumped?.Invoke();
        }

        if (events.Footstep)
        {
            Footstep?.Invoke();
        }

        if (events.Landed is { } landing)
        {
            _lastLanding = landing;
            PlayerCamera.OnLanded(landing);
            Landed?.Invoke(landing);
            if (landing.Type == LandingType.Deadly)
            {
                Log.Info("Runner", $"Deadly landing at {landing.ImpactSpeed:0.0} m/s → respawn");
                Respawn();
                return;
            }
        }

        if (GlobalPosition.Y < KillPlaneY)
        {
            Respawn();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (OS.IsDebugBuild() && @event.IsActionPressed(InputActions.DevRespawn))
        {
            Respawn();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_dev is not { Draw.Enabled: true } dev)
        {
            return;
        }

        ref readonly MotorState s = ref Motor.State;
        Vector3 feet = GlobalPosition;
        dev.Draw.Capsule(feet, Tuning.CapsuleRadius, Tuning.CapsuleHeight, s.IsGrounded ? Colors.LimeGreen : Colors.Orange);
        dev.Draw.Arrow(feet + Vector3.Up * 0.05f, new Vector3(s.Velocity.X, 0, s.Velocity.Z) * 0.25f, Colors.Cyan);
        if (s.IsGrounded)
        {
            dev.Draw.Arrow(feet, s.GroundNormal * 0.6f, Colors.Magenta);
        }
    }

    public void Respawn()
    {
        Motor.Teleport(_spawn.Origin);
        PreviousTickPosition = CurrentTickPosition = _spawn.Origin;
        PlayerInput.SetLook(_spawn.Basis.GetEuler().Y, 0f);
        PlayerCamera.ResetEffects();
        Respawned?.Invoke();
    }

    public void AppendDebugInfo(StringBuilder sb)
    {
        ref readonly MotorState s = ref Motor.State;
        sb.Append("tick ").Append(s.Tick).Append("   mode ").Append(s.Mode).Append(s.IsGrounded ? "   GROUNDED" : "   AIR").AppendLine();
        sb.Append("pos  ").Append(Fmt(s.Position)).AppendLine();
        sb.Append("vel  ").Append(Fmt(s.Velocity)).AppendLine();
        sb.Append("h-speed ").Append(s.HorizontalSpeed.ToString("0.00")).Append(" m/s   v-speed ").Append(s.Velocity.Y.ToString("0.00")).AppendLine(" m/s");
        sb.Append("air ").Append(s.AirTime.ToString("0.00")).Append("s   coyote ").Append(s.TimeSinceGrounded <= Tuning.CoyoteTime && !s.HasJumpedSinceGrounded ? "ok" : "—")
          .Append("   jump buf ").Append(s.JumpBufferRemaining.ToString("0.00")).Append("s   recover ").Append(s.RecoveryRemaining.ToString("0.00")).AppendLine("s");
        sb.Append("last landing ");
        if (_lastLanding is { } l)
        {
            sb.Append(l.Type).Append(" @ ").Append(l.ImpactSpeed.ToString("0.0")).Append(" m/s (").Append((l.Intensity * 100f).ToString("0")).AppendLine("%)");
        }
        else
        {
            sb.AppendLine("—");
        }

        sb.AppendLine("flow —   traversal —   target —   pursuer —");
    }

    private void ApplyCollisionShape()
    {
        if (CollisionShape.Shape is not CapsuleShape3D capsule)
        {
            capsule = new CapsuleShape3D();
            CollisionShape.Shape = capsule;
        }

        capsule.Radius = Tuning.CapsuleRadius;
        capsule.Height = Tuning.CapsuleHeight;
        CollisionShape.Position = new Vector3(0, Tuning.CapsuleHeight * 0.5f, 0);
    }

    private static string Fmt(Vector3 v) => $"({v.X,7:0.00} {v.Y,7:0.00} {v.Z,7:0.00})";
}
