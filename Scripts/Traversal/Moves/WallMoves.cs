using Godot;
using Veilrun.Player;

namespace Veilrun.Traversal;

/// <summary>
/// Horizontal wall run: starts automatically when airborne, fast, pushing forward and moving
/// along a wall at a shallow angle. Low custom gravity (an arc along the wall), follows
/// curved walls, wall steps. Jump = wall jump away from the wall; crouch / releasing forward
/// / time out / end of wall = detach. The same wall cannot be re-run immediately.
/// </summary>
internal static class WallRunMove
{
    internal static bool TryStart(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        if (s.IsCrouched || input.Forward < 0.5f || s.Velocity.Y < t.WallRunMinEntryVerticalSpeed)
        {
            return false;
        }

        var flat = new Vector3(s.Velocity.X, 0f, s.Velocity.Z);
        float speed = flat.Length();
        if (speed < t.WallRunMinSpeed)
        {
            return false;
        }

        Vector3 dir = flat / speed;
        Vector3 rightOfTravel = dir.Cross(Vector3.Up);
        float maxInto = Mathf.Sin(Mathf.DegToRad(t.WallRunMaxEntryAngle));
        Vector3 feet = c.Motor.BodyPosition;
        for (int i = 0; i < 2; i++)
        {
            sbyte side = i == 0 ? (sbyte)1 : (sbyte)-1;
            ProbeHit hit = c.Probes.ProbeSideWall(feet, rightOfTravel * side, c.Move.CapsuleRadius + t.WallRunReach);
            if (!hit.Valid)
            {
                continue;
            }

            Vector3 n = hit.Normal;
            float into = dir.Dot(-n);
            if (into > maxInto || into < -0.2f || (s.WallCooldown > 0f && n.Dot(s.LastWallNormal) > 0.9f))
            {
                continue;
            }

            Vector3 tangent = flat - n * flat.Dot(n);
            tangent.Y = 0f;
            if (tangent.LengthSquared() < 0.01f)
            {
                continue;
            }

            s.TravNormal = n;
            s.TravDir = tangent.Normalized();
            s.TravSpeed = speed;
            s.WallSide = side;
            s.Velocity = new Vector3(s.Velocity.X, Mathf.Max(s.Velocity.Y, t.WallRunEntryUpSpeed), s.Velocity.Z);
            c.Begin(ref s, TraversalKind.WallRun, ref e);
            return true;
        }

        return false;
    }

    internal static void Tick(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        PlayerMotor motor = c.Motor;
        float dt = c.Dt;
        Vector3 n = s.TravNormal;
        Vector3 tangent = s.TravDir;

        if (s.JumpBufferRemaining > 0f)
        {
            Vector3 jump = tangent * (s.TravSpeed * t.WallJumpSpeedRetention) + n * t.WallJumpOutSpeed + Vector3.Up * t.WallJumpUpSpeed;
            s.JumpBufferRemaining = 0f;
            s.HasJumpedSinceGrounded = true;
            s.TimeSinceGrounded = 1f;
            e.Jumped = e.WallJumped = true;
            Finish(c, ref s, ref e);
            s.Velocity = motor.MoveBody(ref s, jump);
            motor.UpdateGroundTimers(ref s, dt);
            return;
        }

        if (input.WasPressed(InputButtons.Crouch) || input.Forward < 0.2f || s.TraversalTime > t.WallRunMaxTime
            || s.TravSpeed < t.WallRunMinSpeed * 0.6f)
        {
            s.Velocity = tangent * s.TravSpeed + n + Vector3.Up * s.Velocity.Y;
            Finish(c, ref s, ref e);
            return;
        }

        s.TravSpeed = Mathf.Max(0f, s.TravSpeed - t.WallRunSpeedDecay * dt);
        float vy = s.Velocity.Y - t.WallRunGravity * dt;
        Vector3 moved = motor.MoveBody(ref s, tangent * s.TravSpeed - n * t.WallStickSpeed + Vector3.Up * vy);
        if (s.IsGrounded)
        {
            Finish(c, ref s, ref e);
            s.Velocity = new Vector3(moved.X, 0f, moved.Z);
            motor.HandleLanding(ref s, Mathf.Max(0f, -vy), input, ref e);
            motor.UpdateGroundTimers(ref s, dt);
            return;
        }

        s.Velocity = tangent * s.TravSpeed + Vector3.Up * moved.Y;
        s.TimeSinceGrounded += dt;
        s.AirTime += dt;

        // Follow the wall (and its curvature); running off its end releases with momentum intact.
        ProbeHit hit = c.Probes.ProbeSideWall(motor.BodyPosition, -n, c.Move.CapsuleRadius + t.WallRunReach + 0.1f);
        if (!hit.Valid)
        {
            Finish(c, ref s, ref e);
            return;
        }

        Vector3 projected = tangent - hit.Normal * tangent.Dot(hit.Normal);
        projected.Y = 0f;
        s.TravNormal = hit.Normal;
        s.TravDir = projected.LengthSquared() > 1e-4f ? projected.Normalized() : tangent;
        motor.AdvanceStride(ref s, s.TravSpeed, dt, ref e);
    }

    private static void Finish(TraversalContext c, ref MotorState s, ref MotorEvents e)
    {
        s.WallCooldown = c.T.SameWallCooldown;
        s.LastWallNormal = s.TravNormal;
        s.WallSide = 0;
        c.End(ref s, ref e);
    }
}

/// <summary>
/// Vertical wall climb ("wall run upward"): jump while running into a wall too tall to mantle.
/// Runs up with decaying speed; catches a ledge (mantle or hang) if one comes within reach;
/// jump = wall kick backwards (the view turns around).
/// </summary>
internal static class WallClimbMove
{
    internal static bool TryStart(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        if (input.Forward < 0.5f)
        {
            return false;
        }

        FrontProbe f = c.ProbeFront(input.WishDir, Mathf.Max(c.FrontReach(s), t.WallClimbReach));
        if (!f.HitWall || f.WallDistance > t.WallClimbReach || (f.HasTop && f.Height <= t.MantleMaxHeight))
        {
            return false;
        }

        Vector3 n = f.WallNormal;
        float toward = new Vector3(s.Velocity.X, 0f, s.Velocity.Z).Dot(-n);
        if (input.Facing.Dot(-n) < t.WallClimbMinFacingDot || toward < t.WallClimbMinSpeed
            || (s.WallCooldown > 0f && n.Dot(s.LastWallNormal) > 0.9f))
        {
            return false;
        }

        s.TravNormal = n;
        s.TravDir = -n;
        s.JumpBufferRemaining = 0f;
        s.HasJumpedSinceGrounded = true;
        s.IsGrounded = false;
        s.IsCrouched = false;
        e.Jumped = true;
        c.Begin(ref s, TraversalKind.WallClimb, ref e);
        return true;
    }

    internal static void Tick(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        PlayerMotor motor = c.Motor;
        float dt = c.Dt;
        Vector3 n = s.TravNormal;

        if (s.JumpBufferRemaining > 0f)
        {
            s.JumpBufferRemaining = 0f;
            e.Jumped = e.WallJumped = e.TurnAround = true;
            Finish(c, ref s, ref e);
            s.Velocity = motor.MoveBody(ref s, n * t.WallKickOutSpeed + Vector3.Up * t.WallKickUpSpeed);
            motor.UpdateGroundTimers(ref s, dt);
            return;
        }

        float remaining = 1f - s.TraversalTime / t.WallClimbTime;
        if (remaining <= 0f)
        {
            s.Velocity = n * 0.5f;
            Finish(c, ref s, ref e);
            return;
        }

        Vector3 moved = motor.MoveBody(ref s, -n * t.WallStickSpeed + Vector3.Up * (t.WallClimbSpeed * remaining));
        s.Velocity = Vector3.Up * moved.Y;
        s.TimeSinceGrounded += dt;
        s.AirTime += dt;
        motor.AdvanceStride(ref s, Mathf.Max(0f, moved.Y), dt, ref e);
        if (s.IsGrounded || moved.Y <= 0.01f)
        {
            Finish(c, ref s, ref e); // head hit something, or somehow back on the floor
            return;
        }

        // Ledge within reach? Mantle straight onto it or catch it.
        c.ForgetFront();
        FrontProbe f = c.ProbeFront(-n, t.WallClimbReach);
        if (!f.HitWall)
        {
            Finish(c, ref s, ref e);
            return;
        }

        if (f.HasTop)
        {
            TraversalKind climbing = s.Traversal;
            if (ObstacleMoves.TryMantle(c, ref s, f, fromAir: true, ref e) || (f.Height <= t.LedgeGrabMaxHeight && LedgeHangMove.TryStart(c, ref s, f, ref e)))
            {
                e.Ended = climbing;
            }
        }
    }

    private static void Finish(TraversalContext c, ref MotorState s, ref MotorEvents e)
    {
        s.WallCooldown = c.T.SameWallCooldown;
        s.LastWallNormal = s.TravNormal;
        c.End(ref s, ref e);
    }
}
