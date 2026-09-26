using Godot;
using Veilrun.Player;

namespace Veilrun.Traversal;

/// <summary>
/// Vault (thin, low obstacle taken at speed), mantle / quick climb (climb onto a standable
/// top) and the shared scripted-path tick also used by the ledge climb. Scripted moves place
/// the body along a deterministic path (validated with clearance checks before starting)
/// instead of simulating collisions, which keeps them smooth and rollback-safe.
/// </summary>
internal static class ObstacleMoves
{
    internal static bool TryStartFromGround(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        if (!WantsToTraverse(c, input))
        {
            return false;
        }

        float reach = c.FrontReach(s);
        FrontProbe f = c.ProbeFront(input.WishDir, reach);
        if (!f.HitWall || !f.HasTop || f.WallDistance > reach)
        {
            return false;
        }

        return TryVault(c, ref s, f, ref e) || TryMantle(c, ref s, f, fromAir: false, ref e);
    }

    internal static bool TryStartFromAir(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        if (!WantsToTraverse(c, input))
        {
            return false;
        }

        float reach = c.FrontReach(s);
        FrontProbe f = c.ProbeFront(input.WishDir, reach);
        if (!f.HitWall || !f.HasTop || f.WallDistance > reach)
        {
            return false;
        }

        if (TryVault(c, ref s, f, ref e) || TryMantle(c, ref s, f, fromAir: true, ref e))
        {
            return true;
        }

        // Hands catch higher ledges — but only from the apex down, so a rising jump keeps
        // rising until the ledge becomes mantle-able instead of snapping down to a hang.
        bool rising = s.Velocity.Y > 0.5f;
        if (!rising && f.Height > c.T.AirMantleMaxHeight && f.Height <= c.T.LedgeGrabMaxHeight
            && s.LedgeCooldown <= 0f && s.Velocity.Y > -12f)
        {
            return LedgeHangMove.TryStart(c, ref s, f, ref e);
        }

        return false;
    }

    private static bool WantsToTraverse(TraversalContext c, in MoveInput input) =>
        input.Strength >= c.T.MinForwardInput && input.WishDir.Dot(input.Facing) >= 0.5f;

    internal static bool TryVault(TraversalContext c, ref MotorState s, in FrontProbe f, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        float speed = s.HorizontalSpeed;
        if (f.Standable || !f.HasFarEdge || f.Depth > t.VaultMaxDepth
            || f.Height < t.VaultMinHeight || f.Height > t.VaultMaxHeight || speed < t.VaultMinSpeed)
        {
            return false;
        }

        float radius = c.Move.CapsuleRadius;
        Vector3 feet = c.Motor.BodyPosition;
        Vector3 dir = -f.WallNormal;
        float faceDistance = TraversalContext.HorizontalDistanceToFace(feet, f);
        float peak = f.TopY + t.VaultClearance;
        float length = faceDistance + f.Depth + radius + 0.2f;
        float split = Mathf.Max(0f, faceDistance - radius - 0.05f);
        float along = Mathf.Max(speed, t.VaultMinSpeed);
        var start = new Vector3(feet.X, 0f, feet.Z);
        Vector3 over = start + dir * (faceDistance + f.Depth * 0.5f) + Vector3.Up * peak;
        Vector3 end = start + dir * length + Vector3.Up * peak;
        if (!c.Probes.CapsuleFree(over, c.Move.CapsuleHeight, radius) || !c.Probes.CapsuleFree(end, c.Move.CapsuleHeight, radius))
        {
            return false;
        }

        s.TravStart = feet;
        s.TravEnd = end;
        s.TravPeakY = peak;
        s.TravSplit = length > 1e-4f ? split / length : 0f;
        s.TravRiseTime = Mathf.Max(split / along, t.VaultRiseTimePerMeter * Mathf.Max(0.2f, peak - feet.Y));
        s.TravMoveTime = (length - split) / along;
        s.TravDir = dir;
        s.TravNormal = f.WallNormal;
        // Slow vaults get a small push out, never a sudden burst: exit ≤ entry + 2 m/s.
        s.TravSpeed = Mathf.Max(speed * t.VaultSpeedRetention, Mathf.Min(t.VaultMinExitSpeed, speed + 2f));
        s.IsGrounded = false;
        s.IsCrouched = false;
        c.Begin(ref s, TraversalKind.Vault, ref e);
        return true;
    }

    internal static bool TryMantle(TraversalContext c, ref MotorState s, in FrontProbe f, bool fromAir, ref MotorEvents e)
    {
        TraversalTuning t = c.T;
        float maxHeight = fromAir ? t.AirMantleMaxHeight : t.MantleMaxHeight;
        if (!f.Standable || f.Height < t.MantleMinHeight || f.Height > maxHeight)
        {
            return false;
        }

        float radius = c.Move.CapsuleRadius;
        Vector3 feet = c.Motor.BodyPosition;
        var wall = new Vector3(f.WallPoint.X, 0f, f.WallPoint.Z);
        Vector3 end = wall - f.WallNormal * (radius + 0.2f) + Vector3.Up * (f.TopY + 0.02f);
        bool crouched = false;
        if (!c.Motor.CanStandAt(end))
        {
            if (!c.Motor.CanCrouchAt(end))
            {
                return false;
            }

            crouched = true;
        }

        float speed = s.HorizontalSpeed;
        bool quick = !fromAir && f.Height <= t.QuickClimbMaxHeight;
        float rise = t.MantleRiseTimeBase + t.MantleRiseTimePerMeter * Mathf.Max(0f, f.TopY - feet.Y);
        if (quick)
        {
            rise *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp(speed / c.Move.SprintSpeed, 0f, 1f));
        }

        var flatStart = new Vector3(feet.X, 0f, feet.Z);
        float length = (new Vector3(end.X, 0f, end.Z) - flatStart).Length();
        float faceDistance = TraversalContext.HorizontalDistanceToFace(feet, f);
        float split = Mathf.Max(0f, faceDistance - radius - 0.03f);

        s.TravStart = feet;
        s.TravEnd = end;
        s.TravPeakY = f.TopY + 0.05f;
        s.TravSplit = length > 1e-4f ? Mathf.Clamp(split / length, 0f, 1f) : 0f;
        s.TravRiseTime = rise;
        s.TravMoveTime = t.MantleForwardTime * (quick ? 0.7f : 1f);
        s.TravDir = -f.WallNormal;
        s.TravNormal = f.WallNormal;
        float retention = f.Height <= t.StepUpMaxHeight ? 1f : t.QuickClimbSpeedRetention;
        s.TravSpeed = quick ? Mathf.Max(speed * retention, t.MantleExitSpeed) : t.MantleExitSpeed;
        s.IsGrounded = false;
        s.IsCrouched = crouched;
        c.Begin(ref s, TraversalKind.Mantle, ref e);
        return true;
    }

    /// <summary>Advance a scripted move (Vault / Mantle / LedgeClimb) and finish it at the end of its path.</summary>
    internal static void TickScripted(TraversalContext c, ref MotorState s, in MoveInput input, ref MotorEvents e)
    {
        PlayerMotor motor = c.Motor;
        float total = TraversalContext.PathDuration(s);
        Vector3 previous = motor.BodyPosition;
        Vector3 position = TraversalContext.EvaluatePath(s, Mathf.Min(s.TraversalTime, total));
        motor.SetBodyPosition(position);
        s.Velocity = (position - previous) / c.Dt;
        if (s.TraversalTime < total)
        {
            return;
        }

        bool landsOnTop = s.Traversal != TraversalKind.Vault;
        s.Velocity = s.TravDir * s.TravSpeed;
        s.IsGrounded = landsOnTop;
        s.GroundNormal = Vector3.Up;
        // Leaving a vault / climb grants a fresh coyote window: vault-into-jump is a core chain.
        s.TimeSinceGrounded = 0f;
        s.AirTime = 0f;
        s.HasJumpedSinceGrounded = false;
        motor.SyncBodyContactState(position, landsOnTop);
        c.End(ref s, ref e);
    }
}
