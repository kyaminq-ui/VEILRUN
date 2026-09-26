using Godot;

namespace Veilrun.Player;

/// <summary>
/// Kinematic, tick-based locomotion. Owns no nodes: it drives a CharacterBody3D for
/// collision only and keeps all gameplay state in <see cref="MotorState"/>.
///
/// Contract (see docs/TDD.md, docs/NETWORKING.md):
///  - Simulate() consumes exactly one InputCommand and advances exactly one physics tick.
///  - Output depends only on (State, command, tuning, static world) → replayable.
///  - Must be called during a physics frame, because MoveAndSlide integrates over the
///    physics step; dt is therefore always the physics step.
/// </summary>
public sealed class PlayerMotor
{
    private readonly CharacterBody3D _body;
    private MotorState _state;

    public PlayerMotor(CharacterBody3D body, MovementTuning tuning)
    {
        _body = body;
        Tuning = tuning;
        ApplyBodyConfiguration();
        _state = new MotorState { Position = body.GlobalPosition, GroundNormal = Vector3.Up };
    }

    public MovementTuning Tuning { get; }

    public ref readonly MotorState State => ref _state;

    /// <summary>Configure CharacterBody3D from tuning so the scene never disagrees with the data.</summary>
    public void ApplyBodyConfiguration()
    {
        _body.MotionMode = CharacterBody3D.MotionModeEnum.Grounded;
        _body.UpDirection = Vector3.Up;
        _body.FloorMaxAngle = Tuning.MaxFloorAngleRadians;
        _body.FloorSnapLength = Tuning.FloorSnapLength;
        _body.FloorConstantSpeed = true;
        _body.FloorStopOnSlope = true;
        _body.FloorBlockOnWall = true;
        _body.SlideOnCeiling = true;
    }

    /// <summary>Snapshot for prediction history / rollback.</summary>
    public MotorState CaptureState()
    {
        MotorState snapshot = _state;
        snapshot.Position = _body.GlobalPosition;
        return snapshot;
    }

    /// <summary>Restore an exact prior state (reconciliation, respawn, tests).</summary>
    public void RestoreState(in MotorState state)
    {
        _state = state;
        SyncBodyContactState(state.Position, state.IsGrounded);
        _body.Velocity = state.Velocity;
    }

    /// <summary>
    /// CharacterBody3D keeps a private "was on floor" flag that decides whether the next
    /// MoveAndSlide snaps to the floor. It has no setter, so after a rollback we re-derive it
    /// with a tiny probe move toward (grounded) or away from (airborne) the floor, then put
    /// the body back at the exact snapshot position. Without this, replays diverge.
    /// </summary>
    private void SyncBodyContactState(Vector3 position, bool grounded)
    {
        _body.GlobalPosition = position;
        _body.Velocity = grounded ? Vector3.Down : Vector3.Up;
        _body.MoveAndSlide();
        _body.GlobalPosition = position;
    }

    public void Teleport(Vector3 position, bool resetVelocity = true)
    {
        MotorState s = _state;
        s.Position = position;
        if (resetVelocity)
        {
            s.Velocity = Vector3.Zero;
            s.JumpBufferRemaining = 0f;
            s.RecoveryRemaining = 0f;
            s.AirTime = 0f;
            s.TimeSinceGrounded = 0f;
            s.IsGrounded = false;
            s.HasJumpedSinceGrounded = false;
        }

        RestoreState(s);
    }

    public MotorEvents Simulate(in InputCommand cmd, float dt)
    {
        MovementTuning t = Tuning;
        MotorState s = _state;
        var events = new MotorEvents();

        // --- Input edges & buffers ---
        InputButtons pressed = cmd.Buttons & ~s.PreviousButtons;
        s.JumpBufferRemaining = (pressed & InputButtons.Jump) != 0
            ? t.JumpBufferTime
            : Mathf.Max(0f, s.JumpBufferRemaining - dt);

        // --- Wish direction in world space (yaw only; pitch never affects ground motion) ---
        Vector2 move = cmd.ClampedMove;
        float moveStrength = move.Length();
        Vector2 forward = new(-Mathf.Sin(cmd.Yaw), -Mathf.Cos(cmd.Yaw));
        Vector2 right = new(Mathf.Cos(cmd.Yaw), -Mathf.Sin(cmd.Yaw));
        Vector2 wish = right * move.X + forward * move.Y;
        Vector2 wishDir = wish.LengthSquared() > 1e-6f ? wish.Normalized() : Vector2.Zero;

        Vector2 horizontal = new(s.Velocity.X, s.Velocity.Z);
        float vertical = s.Velocity.Y;

        // --- Jump (grounded, or within coyote window after walking off) ---
        bool canJump = s.IsGrounded || (s.TimeSinceGrounded <= t.CoyoteTime && !s.HasJumpedSinceGrounded);
        if (s.JumpBufferRemaining > 0f && canJump)
        {
            vertical = t.JumpVelocity;
            s.JumpBufferRemaining = 0f;
            s.HasJumpedSinceGrounded = true;
            s.IsGrounded = false;
            events.Jumped = true;
        }

        // --- Horizontal ---
        if (s.IsGrounded)
        {
            float target = LocomotionMath.TopSpeedFor(wishDir.Dot(forward), moveStrength, t);
            if (s.RecoveryRemaining > 0f)
            {
                target *= s.RecoveryMoveMultiplier;
            }

            horizontal = LocomotionMath.StepGround(horizontal, wishDir, target, t, dt);
            vertical = 0f;
        }
        else
        {
            horizontal = LocomotionMath.StepAir(horizontal, wishDir, moveStrength, t, dt);
            // Half-step gravity before and after the move = exact ballistic arc for constant g,
            // so the measured jump apex matches JumpHeight instead of drifting with tick rate.
            vertical = LocomotionMath.ApplyGravity(vertical, t, dt * 0.5f);
        }

        // --- Move & collide ---
        bool wasGrounded = s.IsGrounded;
        float preMoveVertical = vertical;
        _body.Velocity = new Vector3(horizontal.X, vertical, horizontal.Y);
        _body.MoveAndSlide();
        Vector3 v = _body.Velocity;

        s.IsGrounded = _body.IsOnFloor();
        s.GroundNormal = s.IsGrounded ? _body.GetFloorNormal() : Vector3.Up;

        if (!s.IsGrounded)
        {
            if (_body.IsOnCeiling() && v.Y > 0f)
            {
                v.Y = 0f;
            }

            v.Y = LocomotionMath.ApplyGravity(v.Y, t, dt * 0.5f);
        }

        // --- Landing ---
        if (!wasGrounded && s.IsGrounded)
        {
            float impact = Mathf.Max(0f, -preMoveVertical);
            LandingType type = LocomotionMath.ClassifyLanding(impact, t);
            (float retention, float recovery, float moveMultiplier) = type switch
            {
                LandingType.Medium => (t.MediumSpeedRetention, t.MediumRecoveryTime, t.MediumMoveMultiplier),
                LandingType.Heavy or LandingType.Deadly => (t.HeavySpeedRetention, t.HeavyRecoveryTime, t.HeavyMoveMultiplier),
                _ => (1f, 0f, 1f),
            };
            if (recovery > 0f)
            {
                s.RecoveryRemaining = recovery;
                s.RecoveryMoveMultiplier = moveMultiplier;
            }

            v.X *= retention;
            v.Z *= retention;
            v.Y = 0f;
            s.StrideCycle = 0f;
            events.Landed = new LandingEvent(type, impact, LocomotionMath.LandingIntensity(impact, t), _body.GlobalPosition);
        }
        else if (wasGrounded && !s.IsGrounded && !events.Jumped)
        {
            events.LeftGround = true;
        }

        // --- Timers ---
        if (s.IsGrounded)
        {
            s.TimeSinceGrounded = 0f;
            s.AirTime = 0f;
            s.HasJumpedSinceGrounded = false;
        }
        else
        {
            s.TimeSinceGrounded += dt;
            s.AirTime += dt;
        }

        s.RecoveryRemaining = Mathf.Max(0f, s.RecoveryRemaining - dt);

        // --- Stride: one footstep each time the cycle crosses an integer (2 steps per cycle) ---
        if (s.IsGrounded && events.Landed is null)
        {
            float speed = new Vector2(v.X, v.Z).Length();
            if (speed > 0.3f)
            {
                float next = s.StrideCycle + speed * dt / t.StepLengthAt(speed);
                if (Mathf.FloorToInt(next) != Mathf.FloorToInt(s.StrideCycle))
                {
                    events.Footstep = true;
                }

                s.StrideCycle = next >= 2f ? next - 2f : next;
            }
        }

        s.Velocity = v;
        s.Position = _body.GlobalPosition;
        s.PreviousButtons = cmd.Buttons;
        s.Tick = cmd.Tick;
        s.Mode = ClassifyMode(s, t);

        _body.Velocity = v;
        _state = s;
        return events;
    }

    private static LocomotionMode ClassifyMode(in MotorState s, MovementTuning t)
    {
        if (!s.IsGrounded)
        {
            return LocomotionMode.Airborne;
        }

        if (s.RecoveryRemaining > 0f)
        {
            return LocomotionMode.Recovering;
        }

        float speed = s.HorizontalSpeed;
        if (speed < 0.1f)
        {
            return LocomotionMode.Idle;
        }

        if (speed <= t.WalkSpeed + 0.05f)
        {
            return LocomotionMode.Walk;
        }

        return speed <= t.RunSpeed + 0.05f ? LocomotionMode.Run : LocomotionMode.Sprint;
    }
}
