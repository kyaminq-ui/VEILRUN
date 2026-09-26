using Godot;
using Veilrun.Traversal;

namespace Veilrun.Player;

/// <summary>
/// Kinematic, tick-based runner simulation: ground/air locomotion plus parkour moves
/// (slide, vault, mantle, ledge, wall run, wall climb, roll). Owns no nodes: it drives a
/// CharacterBody3D for collision and keeps ALL gameplay state in <see cref="MotorState"/>.
///
/// Contract (see docs/TDD.md, docs/NETWORKING.md):
///  - Simulate() consumes exactly one InputCommand and advances exactly one physics tick.
///  - Output depends only on (State, command, tuning, static world) → replayable.
///  - Must be called during a physics frame (MoveAndSlide integrates over the physics step).
///
/// Traversal moves live in Veilrun.Traversal as stateless classes; they read/write the
/// MotorState passed by ref and use the services exposed here (MoveBody, landing, capsule).
/// </summary>
public sealed class PlayerMotor
{
    private readonly CharacterBody3D _body;
    private readonly CollisionShape3D _shapeNode;
    private readonly CapsuleShape3D _capsule;
    private readonly TraversalContext _traversal;
    private MotorState _state;
    private float _appliedCapsuleHeight = -1f;

    public PlayerMotor(CharacterBody3D body, MovementTuning tuning, TraversalTuning? traversal = null)
    {
        _body = body;
        Tuning = tuning;
        TraversalTuning = traversal ?? new TraversalTuning();
        (_shapeNode, _capsule) = TakeOwnershipOfCapsule(body);
        Probes = new TraversalProbes(body);
        _traversal = new TraversalContext(this);
        ApplyBodyConfiguration();
        _state = new MotorState { Position = body.GlobalPosition, GroundNormal = Vector3.Up };
        ApplyCapsuleHeight(Tuning.CapsuleHeight);
    }

    public MovementTuning Tuning { get; }

    public TraversalTuning TraversalTuning { get; }

    public TraversalProbes Probes { get; }

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

    // ------------------------------------------------------------------ snapshot API

    public MotorState CaptureState()
    {
        MotorState snapshot = _state;
        snapshot.Position = _body.GlobalPosition;
        return snapshot;
    }

    public void RestoreState(in MotorState state)
    {
        _state = state;
        ApplyCapsuleHeight(CapsuleHeightFor(state));
        SyncBodyContactState(state.Position, state.IsGrounded);
        _body.Velocity = state.Velocity;
    }

    public void Teleport(Vector3 position, bool resetVelocity = true)
    {
        MotorState s = _state;
        s.Position = position;
        if (resetVelocity)
        {
            s = new MotorState
            {
                Tick = s.Tick,
                Position = position,
                GroundNormal = Vector3.Up,
                PreviousButtons = s.PreviousButtons,
            };
        }

        RestoreState(s);
    }

    // ------------------------------------------------------------------ tick

    public MotorEvents Simulate(in InputCommand cmd, float dt)
    {
        MotorState s = _state;
        var e = new MotorEvents();
        var input = new MoveInput(cmd, s.PreviousButtons);
        Probes.BeginTick();
        UpdateBuffersAndCooldowns(ref s, input, dt);
        ApplyCapsuleHeight(CapsuleHeightFor(s));

        if (s.Traversal != TraversalKind.None)
        {
            _traversal.Tick(ref s, input, dt, ref e);
        }
        else if (!_traversal.TryStart(ref s, input, dt, ref e))
        {
            Locomotion(ref s, input, dt, ref e);
        }

        ApplyCapsuleHeight(CapsuleHeightFor(s));
        s.RecoveryRemaining = Mathf.Max(0f, s.RecoveryRemaining - dt);
        s.Position = _body.GlobalPosition;
        s.PreviousButtons = cmd.Buttons;
        s.Tick = cmd.Tick;
        s.Mode = ClassifyMode(s, Tuning);
        _body.Velocity = s.Velocity;
        _state = s;
        return e;
    }

    private void UpdateBuffersAndCooldowns(ref MotorState s, in MoveInput input, float dt)
    {
        s.JumpBufferRemaining = input.WasPressed(InputButtons.Jump)
            ? Tuning.JumpBufferTime
            : Mathf.Max(0f, s.JumpBufferRemaining - dt);
        s.CrouchBufferRemaining = input.WasPressed(InputButtons.Crouch)
            ? TraversalTuning.RollWindow
            : Mathf.Max(0f, s.CrouchBufferRemaining - dt);
        s.SlideCooldown = Mathf.Max(0f, s.SlideCooldown - dt);
        s.LedgeCooldown = Mathf.Max(0f, s.LedgeCooldown - dt);
        s.WallCooldown = Mathf.Max(0f, s.WallCooldown - dt);
    }

    // ------------------------------------------------------------------ locomotion

    private void Locomotion(ref MotorState s, in MoveInput input, float dt, ref MotorEvents e)
    {
        MovementTuning t = Tuning;
        Vector2 horizontal = MoveInput.Flat(s.Velocity);
        float vertical = s.Velocity.Y;

        // Crouch (ground only). Standing up needs head room.
        if (s.IsGrounded && input.IsHeld(InputButtons.Crouch))
        {
            s.IsCrouched = true;
        }
        else if (s.IsCrouched && !input.IsHeld(InputButtons.Crouch) && CanStandAt(_body.GlobalPosition))
        {
            s.IsCrouched = false;
        }

        ApplyCapsuleHeight(CapsuleHeightFor(s));

        // Jump (grounded, or within coyote window after walking off). Crouched jumps must stand first.
        bool canJump = s.IsGrounded || (s.TimeSinceGrounded <= t.CoyoteTime && !s.HasJumpedSinceGrounded);
        if (s.JumpBufferRemaining > 0f && canJump && (!s.IsCrouched || CanStandAt(_body.GlobalPosition)))
        {
            s.IsCrouched = false;
            ApplyCapsuleHeight(CapsuleHeightFor(s));
            vertical = t.JumpVelocity;
            s.JumpBufferRemaining = 0f;
            s.HasJumpedSinceGrounded = true;
            s.IsGrounded = false;
            e.Jumped = true;
        }

        Vector2 wishDir = MoveInput.Flat(input.WishDir);
        if (s.IsGrounded)
        {
            float target = LocomotionMath.TopSpeedFor(input.WishDir.Dot(input.Facing), input.Strength, t);
            if (s.IsCrouched)
            {
                target = Mathf.Min(target, TraversalTuning.CrouchSpeed);
            }

            if (s.RecoveryRemaining > 0f)
            {
                target *= s.RecoveryMoveMultiplier;
            }

            horizontal = LocomotionMath.StepGround(horizontal, wishDir, target, t, dt);
            vertical = 0f;
        }
        else
        {
            horizontal = LocomotionMath.StepAir(horizontal, wishDir, input.Strength, t, dt);
            // Half-step gravity before and after the move = exact ballistic arc for constant g.
            vertical = LocomotionMath.ApplyGravity(vertical, t, dt * 0.5f);
        }

        bool wasGrounded = s.IsGrounded;
        float preMoveVertical = vertical;
        Vector3 v = MoveBody(ref s, MoveInput.Lift(horizontal, vertical));

        if (!s.IsGrounded)
        {
            v.Y = LocomotionMath.ApplyGravity(v.Y, t, dt * 0.5f);
            if (s.IsCrouched && CanStandAt(_body.GlobalPosition))
            {
                s.IsCrouched = false;
            }
        }

        s.Velocity = v;
        if (!wasGrounded && s.IsGrounded)
        {
            HandleLanding(ref s, Mathf.Max(0f, -preMoveVertical), input, ref e);
        }
        else if (wasGrounded && !s.IsGrounded && !e.Jumped)
        {
            e.LeftGround = true;
        }

        UpdateGroundTimers(ref s, dt);
        if (s.IsGrounded && e.Landed is null && s.Traversal == TraversalKind.None)
        {
            AdvanceStride(ref s, s.HorizontalSpeed, dt, ref e);
        }
    }

    // ------------------------------------------------------------------ services for traversal moves

    /// <summary>MoveAndSlide with <paramref name="velocity"/>; updates grounded/normal and returns the post-collision velocity.</summary>
    internal Vector3 MoveBody(ref MotorState s, Vector3 velocity)
    {
        _body.Velocity = velocity;
        _body.MoveAndSlide();
        s.IsGrounded = _body.IsOnFloor();
        s.GroundNormal = s.IsGrounded ? _body.GetFloorNormal() : Vector3.Up;
        Vector3 v = _body.Velocity;
        if (!s.IsGrounded && _body.IsOnCeiling() && v.Y > 0f)
        {
            v.Y = 0f;
        }

        return v;
    }

    /// <summary>Place the body at an exact position (scripted moves). No collision: callers validate clearance first.</summary>
    internal void SetBodyPosition(Vector3 position) => _body.GlobalPosition = position;

    internal Vector3 BodyPosition => _body.GlobalPosition;

    internal void UpdateGroundTimers(ref MotorState s, float dt)
    {
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
    }

    /// <summary>Stride cycle ([0,2), one footstep per integer crossing) — ground and wall runs.</summary>
    internal void AdvanceStride(ref MotorState s, float speed, float dt, ref MotorEvents e)
    {
        if (speed <= 0.3f)
        {
            return;
        }

        float next = s.StrideCycle + speed * dt / Tuning.StepLengthAt(speed);
        if (Mathf.FloorToInt(next) != Mathf.FloorToInt(s.StrideCycle))
        {
            e.Footstep = true;
        }

        s.StrideCycle = next >= 2f ? next - 2f : next;
    }

    /// <summary>Classify an impact, apply the landing tier (or a landing roll), publish the event.</summary>
    internal void HandleLanding(ref MotorState s, float impact, in MoveInput input, ref MotorEvents e)
    {
        MovementTuning t = Tuning;
        LandingType type = LocomotionMath.ClassifyLanding(impact, t);
        float intensity = LocomotionMath.LandingIntensity(impact, t);
        Vector3 v = s.Velocity;
        v.Y = 0f;
        s.Velocity = v;
        s.StrideCycle = 0f;

        bool rollable = type is LandingType.Medium or LandingType.Heavy;
        if (rollable && s.CrouchBufferRemaining > 0f)
        {
            s.CrouchBufferRemaining = 0f;
            e.Landed = new LandingEvent(type, impact, intensity, _body.GlobalPosition, Rolled: true);
            _traversal.StartRoll(ref s, input, ref e);
            return;
        }

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

        s.Velocity = new Vector3(v.X * retention, 0f, v.Z * retention);
        e.Landed = new LandingEvent(type, impact, intensity, _body.GlobalPosition);

        // Crouch buffered into a soft landing at speed = land straight into a slide.
        if (type == LandingType.Soft && s.CrouchBufferRemaining > 0f)
        {
            _traversal.TryStartSlide(ref s, input, ref e, requirePress: false);
        }
    }

    // ------------------------------------------------------------------ capsule

    public float CapsuleHeightFor(in MotorState s) => s.Traversal switch
    {
        TraversalKind.Slide or TraversalKind.Roll => TraversalTuning.SlideCapsuleHeight,
        _ => s.IsCrouched ? TraversalTuning.CrouchCapsuleHeight : Tuning.CapsuleHeight,
    };

    /// <summary>Camera eye height (feet-relative) for the current posture.</summary>
    public float EyeHeightFor(in MotorState s) => s.Traversal switch
    {
        TraversalKind.Slide or TraversalKind.Roll => TraversalTuning.SlideEyeHeight,
        _ => s.IsCrouched ? TraversalTuning.CrouchEyeHeight : Tuning.EyeHeight,
    };

    internal bool CanStandAt(Vector3 feet) => Probes.CapsuleFree(feet, Tuning.CapsuleHeight, Tuning.CapsuleRadius);

    internal bool CanCrouchAt(Vector3 feet) => Probes.CapsuleFree(feet, TraversalTuning.CrouchCapsuleHeight, Tuning.CapsuleRadius);

    internal void ApplyCapsuleHeight(float height)
    {
        if (Mathf.IsEqualApprox(height, _appliedCapsuleHeight))
        {
            return;
        }

        _appliedCapsuleHeight = height;
        _capsule.Radius = Tuning.CapsuleRadius;
        _capsule.Height = height;
        _shapeNode.Position = new Vector3(0f, height * 0.5f, 0f);
    }

    /// <summary>The motor resizes the capsule, so each runner must own its shape (scene sub-resources are shared).</summary>
    private static (CollisionShape3D, CapsuleShape3D) TakeOwnershipOfCapsule(CharacterBody3D body)
    {
        foreach (Node child in body.GetChildren())
        {
            if (child is CollisionShape3D node)
            {
                var capsule = node.Shape is CapsuleShape3D existing ? (CapsuleShape3D)existing.Duplicate() : new CapsuleShape3D();
                node.Shape = capsule;
                return (node, capsule);
            }
        }

        var created = new CollisionShape3D { Name = "Collision", Shape = new CapsuleShape3D() };
        body.AddChild(created);
        return (created, (CapsuleShape3D)created.Shape);
    }

    // ------------------------------------------------------------------ rollback helpers

    /// <summary>
    /// CharacterBody3D keeps a private "was on floor" flag that decides whether the next
    /// MoveAndSlide snaps to the floor. It has no setter, so after a rollback (or a scripted
    /// move) we re-derive it with a tiny probe move toward (grounded) or away from (airborne)
    /// the floor, then put the body back at the exact position. Without this, replays diverge.
    /// </summary>
    internal void SyncBodyContactState(Vector3 position, bool grounded)
    {
        // The grounded probe must reach as far as the floor snap would (scripted moves end a
        // couple of cm above the surface); otherwise the result depends on the stale flag.
        float delta = Mathf.Max((float)_body.GetPhysicsProcessDeltaTime(), 1e-3f);
        _body.GlobalPosition = position;
        _body.Velocity = grounded ? Vector3.Down * ((Tuning.FloorSnapLength + 0.05f) / delta) : Vector3.Up;
        _body.MoveAndSlide();
        _body.GlobalPosition = position;
    }

    private static LocomotionMode ClassifyMode(in MotorState s, MovementTuning t)
    {
        if (s.Traversal != TraversalKind.None)
        {
            return LocomotionMode.Traversal;
        }

        if (!s.IsGrounded)
        {
            return LocomotionMode.Airborne;
        }

        if (s.RecoveryRemaining > 0f)
        {
            return LocomotionMode.Recovering;
        }

        if (s.IsCrouched)
        {
            return LocomotionMode.Crouch;
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
