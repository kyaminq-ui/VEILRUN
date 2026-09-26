using Godot;
using Veilrun.Player;

namespace Veilrun.Traversal;

/// <summary>
/// Slide: crouch pressed at speed. Low capsule, small boost, friction, gravity along slopes,
/// weak steering. Jump = slide jump (keeps the slide's momentum). Ends when slow, when crouch
/// is released, or when sliding off an edge; stays crouched if there is no head room.
/// </summary>
internal static class SlideMove
{
    private const float SlopeGravity = 9.81f;

    internal static bool TryStart(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e, bool requirePress)
    {
        TraversalTuning t = c.T;
        if ((requirePress && !input.WasPressed(InputButtons.Crouch)) || s.SlideCooldown > 0f || s.HorizontalSpeed < t.SlideMinSpeed)
        {
            return false;
        }

        var flat = new Vector3(s.Velocity.X, 0f, s.Velocity.Z);
        s.TravDir = flat.Normalized();
        s.TravSpeed = Mathf.Min(s.HorizontalSpeed + t.SlideBoost, Mathf.Max(t.SlideMaxSpeed, s.HorizontalSpeed));
        s.CrouchBufferRemaining = 0f;
        s.IsCrouched = false;
        c.Begin(ref s, TraversalKind.Slide, ref e);
        return true;
    }

    internal static void Tick(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        PlayerMotor motor = c.Motor;
        float dt = c.Dt;

        // Slide jump: stand up (needs head room) and jump with the slide's momentum.
        if (s.JumpBufferRemaining > 0f && motor.CanStandAt(motor.BodyPosition))
        {
            Finish(c, ref s, ref e, crouched: false);
            s.JumpBufferRemaining = 0f;
            s.HasJumpedSinceGrounded = true;
            s.IsGrounded = false;
            e.Jumped = true;
            motor.ApplyCapsuleHeight(motor.CapsuleHeightFor(s));
            s.Velocity = motor.MoveBody(ref s, s.TravDir * s.TravSpeed + Vector3.Up * c.Move.JumpVelocity);
            motor.UpdateGroundTimers(ref s, dt);
            return;
        }

        // Weak steering toward the input.
        if (input.WishDir != Vector3.Zero)
        {
            float angle = Mathf.Clamp(s.TravDir.SignedAngleTo(input.WishDir, Vector3.Up), -t.SlideTurnRate * dt, t.SlideTurnRate * dt);
            s.TravDir = s.TravDir.Rotated(Vector3.Up, angle).Normalized();
        }

        // Friction, plus gravity pulling along the slope (downhill slides accelerate).
        float accel = -t.SlideFriction;
        if (s.IsGrounded)
        {
            Vector3 g = Vector3.Down * SlopeGravity;
            Vector3 alongSlope = g - s.GroundNormal * g.Dot(s.GroundNormal);
            accel += alongSlope.Dot(s.TravDir) * t.SlideSlopeGravityScale;
        }

        s.TravSpeed = Mathf.Clamp(s.TravSpeed + accel * dt, 0f, t.SlideMaxSpeed);
        Vector3 moved = motor.MoveBody(ref s, s.TravDir * s.TravSpeed);
        var flat = new Vector3(moved.X, 0f, moved.Z);
        s.Velocity = flat;

        // Walls redirect the slide along them.
        float actual = flat.Length();
        if (actual < s.TravSpeed - 0.05f)
        {
            s.TravSpeed = actual;
            if (actual > 0.1f)
            {
                s.TravDir = flat / actual;
            }
        }

        if (!s.IsGrounded)
        {
            // Slid off an edge: keep the momentum, stand up in the air if possible.
            Finish(c, ref s, ref e, crouched: !motor.CanStandAt(motor.BodyPosition));
            e.LeftGround = true;
            motor.UpdateGroundTimers(ref s, dt);
            return;
        }

        bool released = !input.IsHeld(InputButtons.Crouch);
        if (s.TravSpeed <= t.SlideExitSpeed || released)
        {
            bool stand = released && motor.CanStandAt(motor.BodyPosition);
            Finish(c, ref s, ref e, crouched: !stand);
        }

        motor.UpdateGroundTimers(ref s, dt);
    }

    private static void Finish(TraversalContext c, ref MotorState s, ref MotorEvents e, bool crouched)
    {
        s.SlideCooldown = c.T.SlideCooldown;
        s.IsCrouched = crouched;
        c.End(ref s, ref e);
    }
}

/// <summary>
/// Landing roll: a Medium/Heavy landing with crouch pressed just before touch-down turns
/// into a short forward roll that keeps most of the momentum and skips the landing penalty.
/// </summary>
internal static class RollMove
{
    internal static void Start(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        var flat = new Vector3(s.Velocity.X, 0f, s.Velocity.Z);
        float speed = flat.Length();
        s.TravDir = speed > 0.5f ? flat / speed : input.Facing;
        s.TravSpeed = Mathf.Max(speed * t.RollSpeedRetention, t.RollMinSpeed);
        s.IsCrouched = false;
        s.Velocity = s.TravDir * s.TravSpeed;
        c.Begin(ref s, TraversalKind.Roll, ref e);
    }

    internal static void Tick(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        PlayerMotor motor = c.Motor;
        float dt = c.Dt;
        if (input.WishDir != Vector3.Zero)
        {
            float angle = Mathf.Clamp(s.TravDir.SignedAngleTo(input.WishDir, Vector3.Up), -t.RollTurnRate * dt, t.RollTurnRate * dt);
            s.TravDir = s.TravDir.Rotated(Vector3.Up, angle).Normalized();
        }

        Vector3 moved = motor.MoveBody(ref s, s.TravDir * s.TravSpeed);
        s.Velocity = new Vector3(moved.X, s.IsGrounded ? 0f : moved.Y, moved.Z);
        if (!s.IsGrounded || s.TraversalTime >= t.RollDuration)
        {
            s.IsCrouched = !motor.CanStandAt(motor.BodyPosition);
            c.End(ref s, ref e);
        }

        motor.UpdateGroundTimers(ref s, dt);
    }
}
