using Godot;
using Veilrun.Player;

namespace Veilrun.Traversal;

/// <summary>
/// Ledge grab: snap to a hang under the ledge, then climb (forward / jump), drop (crouch),
/// eject backwards (back + jump, turns the view) or shimmy sideways along the ledge.
/// The ledge top is derived from the hang position: topY = TravEnd.Y + LedgeHangDrop.
/// </summary>
internal static class LedgeHangMove
{
    internal static bool TryStart(TraversalContext c, ref MotorState s, in FrontProbe f, ref MotorEvents e)
    {
        float radius = c.Move.CapsuleRadius;
        var wall = new Vector3(f.WallPoint.X, 0f, f.WallPoint.Z);
        Vector3 hang = wall + f.WallNormal * (radius + 0.03f) + Vector3.Up * (f.TopY - c.T.LedgeHangDrop);
        if (!c.Probes.CapsuleFree(hang, c.Move.CapsuleHeight, radius))
        {
            return false;
        }

        s.TravStart = c.Motor.BodyPosition;
        s.TravEnd = hang;
        s.TravPeakY = hang.Y;
        s.TravSplit = 1f;
        s.TravRiseTime = c.T.LedgeSnapTime;
        s.TravMoveTime = 0f;
        s.TravNormal = f.WallNormal;
        s.TravDir = -f.WallNormal;
        s.Velocity = Vector3.Zero;
        s.IsGrounded = false;
        s.IsCrouched = false;
        s.JumpBufferRemaining = 0f;
        c.Begin(ref s, TraversalKind.LedgeHang, ref e);
        return true;
    }

    internal static void Tick(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        PlayerMotor motor = c.Motor;
        float dt = c.Dt;
        Vector3 previous = motor.BodyPosition;

        if (s.TraversalTime < t.LedgeSnapTime)
        {
            Vector3 snap = TraversalContext.EvaluatePath(s, s.TraversalTime);
            motor.SetBodyPosition(snap);
            s.Velocity = (snap - previous) / dt;
            return;
        }

        Vector3 pos = s.TravEnd;
        Vector3 n = s.TravNormal;
        float radius = c.Move.CapsuleRadius;
        float topY = pos.Y + t.LedgeHangDrop;
        motor.SetBodyPosition(pos);
        s.Velocity = Vector3.Zero;

        if (input.WasPressed(InputButtons.Crouch))
        {
            Release(c, ref s, ref e, pos, n * t.LedgeDropPush);
            return;
        }

        if (s.JumpBufferRemaining > 0f && input.Forward < -0.3f)
        {
            s.JumpBufferRemaining = 0f;
            e.Jumped = e.WallJumped = e.TurnAround = true;
            Release(c, ref s, ref e, pos, n * t.WallKickOutSpeed + Vector3.Up * t.WallKickUpSpeed);
            return;
        }

        if (input.Forward > 0.5f || s.JumpBufferRemaining > 0f)
        {
            TryClimb(c, ref s, ref e, pos, n, topY, radius);
            return;
        }

        if (Mathf.Abs(input.Move.X) > 0.3f)
        {
            Shimmy(c, ref s, input, pos, n, topY, radius);
        }
    }

    private static void TryClimb(TraversalContext c, ref MotorState s, ref MotorEvents e, Vector3 pos, Vector3 n, float topY, float radius)
    {
        Vector3 top = new Vector3(pos.X, topY + 0.02f, pos.Z) - n * (2f * radius + 0.23f);
        bool crouched = false;
        if (!c.Motor.CanStandAt(top))
        {
            if (!c.Motor.CanCrouchAt(top))
            {
                return; // no room up there: keep hanging
            }

            crouched = true;
        }

        s.JumpBufferRemaining = 0f;
        s.TravStart = pos;
        s.TravEnd = top;
        s.TravPeakY = topY + 0.05f;
        s.TravSplit = 0.1f;
        s.TravRiseTime = c.T.LedgeClimbTime * 0.7f;
        s.TravMoveTime = c.T.LedgeClimbTime * 0.3f;
        s.TravDir = -n;
        s.TravSpeed = c.T.MantleExitSpeed * 0.6f;
        s.IsCrouched = crouched;
        e.Ended = TraversalKind.LedgeHang;
        c.Begin(ref s, TraversalKind.LedgeClimb, ref e);
    }

    private static void Shimmy(TraversalContext c, ref MotorState s, in MoveInput input, Vector3 pos, Vector3 n, float topY, float radius)
    {
        Vector3 right = (-n).Cross(Vector3.Up);
        Vector3 candidate = pos + right * (input.Move.X * c.T.ShimmySpeed * c.Dt);
        Vector3 hands = candidate + Vector3.Up * (c.T.LedgeHangDrop - 0.15f);
        ProbeHit wall = c.Probes.Ray(hands, hands - n * (radius + 0.3f));
        if (!wall.Valid || Mathf.Abs(wall.Normal.Y) > 0.3f)
        {
            return; // ledge face ends here
        }

        Vector3 n2 = new Vector3(wall.Normal.X, 0f, wall.Normal.Z).Normalized();
        Vector3 inset = new Vector3(wall.Point.X, 0f, wall.Point.Z) - n2 * 0.04f;
        ProbeHit top = c.Probes.Ray(inset + Vector3.Up * (topY + 0.3f), inset + Vector3.Up * (topY - 0.3f));
        if (!top.Valid || Mathf.Abs(top.Point.Y - topY) > 0.08f)
        {
            return; // ledge top ends / changes height
        }

        Vector3 next = new Vector3(wall.Point.X, 0f, wall.Point.Z) + n2 * (radius + 0.03f) + Vector3.Up * (top.Point.Y - c.T.LedgeHangDrop);
        if (!c.Probes.CapsuleFree(next, c.Move.CapsuleHeight, radius))
        {
            return;
        }

        c.Motor.SetBodyPosition(next);
        s.Velocity = (next - pos) / c.Dt;
        s.TravEnd = next;
        s.TravNormal = n2;
        s.TravDir = -n2;
    }

    private static void Release(TraversalContext c, ref MotorState s, ref MotorEvents e, Vector3 pos, Vector3 velocity)
    {
        s.LedgeCooldown = c.T.LedgeRegrabCooldown;
        s.HasJumpedSinceGrounded = true;
        s.TimeSinceGrounded = 1f;
        s.IsGrounded = false;
        s.Velocity = velocity;
        c.Motor.SyncBodyContactState(pos, grounded: false);
        c.End(ref s, ref e);
    }
}
