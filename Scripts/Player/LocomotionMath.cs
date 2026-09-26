using Godot;

namespace Veilrun.Player;

/// <summary>
/// Pure, allocation-free locomotion math. No scene tree, no physics queries:
/// everything here is deterministic given its inputs and is unit-tested directly.
/// Horizontal velocities are expressed as Vector2(x, z).
/// </summary>
public static class LocomotionMath
{
    private const float Epsilon = 1e-4f;
    private const float MinSpeedForHeading = 0.05f;

    /// <summary>
    /// Ground horizontal velocity update. Heading and speed are integrated separately:
    /// heading turns at a speed-dependent rate (tight at run speed, wide at sprint),
    /// speed approaches the target with a fast acceleration up to run speed and a slow
    /// one above it, so top speed is something the player builds and keeps.
    /// </summary>
    public static Vector2 StepGround(Vector2 velocity, Vector2 wishDir, float targetSpeed, MovementTuning t, float dt)
    {
        float speed = velocity.Length();
        bool hasWish = targetSpeed > Epsilon && wishDir.LengthSquared() > Epsilon;

        if (!hasWish)
        {
            return MoveTowardZero(velocity, speed, t.GroundDeceleration * dt);
        }

        if (speed < MinSpeedForHeading)
        {
            return wishDir * ApproachSpeed(speed, targetSpeed, t, dt);
        }

        Vector2 heading = velocity / speed;

        // Input pointing mostly backwards = brake in place rather than pivot at full speed.
        if (heading.Dot(wishDir) < t.ReverseDotThreshold)
        {
            return MoveTowardZero(velocity, speed, t.GroundDeceleration * dt);
        }

        float sprintBlend = Mathf.Clamp(Mathf.InverseLerp(t.RunSpeed, t.SprintSpeed, speed), 0f, 1f);
        float maxTurn = Mathf.Lerp(t.TurnRateLowSpeed, t.TurnRateHighSpeed, sprintBlend) * dt;
        float turn = Mathf.Clamp(heading.AngleTo(wishDir), -maxTurn, maxTurn);
        heading = heading.Rotated(turn);

        // Turning bleeds only the speed carried above run speed: sharp lines cost momentum.
        if (speed > t.RunSpeed)
        {
            float retained = Mathf.Max(0f, 1f - t.TurnSpeedLossPerRadian * Mathf.Abs(turn));
            speed = t.RunSpeed + (speed - t.RunSpeed) * retained;
        }

        return heading * ApproachSpeed(speed, targetSpeed, t, dt);
    }

    /// <summary>
    /// Scalar speed approach along the walk → run → sprint bands. Each band has its own
    /// acceleration, so the first step is instant, run arrives quickly and sprint is earned.
    /// A tick never crosses a band boundary (costs at most one tick per boundary, keeps it exact).
    /// </summary>
    public static float ApproachSpeed(float speed, float targetSpeed, MovementTuning t, float dt)
    {
        if (speed < targetSpeed)
        {
            float bandTop, rate;
            if (speed < t.WalkSpeed)
            {
                (bandTop, rate) = (t.WalkSpeed, t.WalkAcceleration);
            }
            else if (speed < t.RunSpeed)
            {
                (bandTop, rate) = (t.RunSpeed, t.RunAcceleration);
            }
            else
            {
                (bandTop, rate) = (t.SprintSpeed, t.SprintAcceleration);
            }

            return Mathf.Min(Mathf.Min(bandTop, targetSpeed), speed + rate * dt);
        }

        // Above target while still pushing: shed overspeed gently so momentum carries.
        return Mathf.Max(targetSpeed, speed - t.OverspeedDeceleration * dt);
    }

    /// <summary>
    /// Speed the runner is allowed to build toward for this input. Forward input builds all the
    /// way to sprint; strafing and backpedalling cap lower; a lightly deflected stick walks.
    /// </summary>
    public static float TopSpeedFor(float forwardDot, float inputStrength, MovementTuning t)
    {
        if (inputStrength <= Epsilon)
        {
            return 0f;
        }

        if (inputStrength < t.AnalogWalkThreshold)
        {
            return t.WalkSpeed * (inputStrength / t.AnalogWalkThreshold);
        }

        if (forwardDot >= t.SprintMinForwardDot)
        {
            return t.SprintSpeed;
        }

        // Blend strafe (dot 0) → sprint (dot = SprintMinForwardDot), and strafe → backpedal behind.
        return forwardDot >= 0f
            ? Mathf.Lerp(t.StrafeMaxSpeed, t.SprintSpeed, forwardDot / t.SprintMinForwardDot)
            : Mathf.Lerp(t.StrafeMaxSpeed, t.BackpedalMaxSpeed, -forwardDot);
    }

    /// <summary>
    /// Air control: the player can steer and add speed up to AirControlMaxSpeed, but can
    /// never exceed the speed they entered the air with if that was higher.
    /// </summary>
    public static Vector2 StepAir(Vector2 velocity, Vector2 wishDir, float wishStrength, MovementTuning t, float dt)
    {
        if (wishStrength <= Epsilon || wishDir.LengthSquared() <= Epsilon)
        {
            return velocity;
        }

        float cap = Mathf.Max(velocity.Length(), t.AirControlMaxSpeed);
        Vector2 result = velocity + wishDir * (t.AirAcceleration * wishStrength * dt);
        float resultSpeed = result.Length();
        return resultSpeed > cap ? result * (cap / resultSpeed) : result;
    }

    /// <summary>Apply asymmetric gravity (lighter on the way up, heavier falling) over dt.</summary>
    public static float ApplyGravity(float verticalVelocity, MovementTuning t, float dt)
    {
        float g = verticalVelocity > 0f ? t.RiseGravity : t.FallGravity;
        return Mathf.Max(verticalVelocity - g * dt, -t.TerminalFallSpeed);
    }

    public static LandingType ClassifyLanding(float impactSpeed, MovementTuning t)
    {
        if (impactSpeed >= t.DeadlyLandingSpeed)
        {
            return LandingType.Deadly;
        }

        if (impactSpeed >= t.HeavyLandingSpeed)
        {
            return LandingType.Heavy;
        }

        return impactSpeed >= t.MediumLandingSpeed ? LandingType.Medium : LandingType.Soft;
    }

    /// <summary>0 = feather-light touch, 1 = deadly impact. Drives camera/audio feedback.</summary>
    public static float LandingIntensity(float impactSpeed, MovementTuning t) =>
        Mathf.Clamp(impactSpeed / t.DeadlyLandingSpeed, 0f, 1f);

    private static Vector2 MoveTowardZero(Vector2 velocity, float speed, float amount) =>
        speed <= amount ? Vector2.Zero : velocity * ((speed - amount) / speed);
}
