using Godot;
using Veilrun.Player;
using Veilrun.Tests.Framework;

namespace Veilrun.Tests;

/// <summary>Pure math tests: no physics, no scene tree.</summary>
public sealed class LocomotionMathTests
{
    private const float Dt = 1f / 60f;
    private readonly MovementTuning _t = new();

    [Test]
    public void JumpDerivationReproducesDesignHeight()
    {
        float apex = _t.JumpVelocity * _t.JumpVelocity / (2f * _t.RiseGravity);
        Check.Near(_t.JumpHeight, apex, 1e-4, "v²/2g must equal JumpHeight");
        Check.Near(_t.JumpTimeToApex, _t.JumpVelocity / _t.RiseGravity, 1e-4, "v/g must equal JumpTimeToApex");
    }

    [Test]
    public void GroundAccelerationFromRestIsCappedAtRunSpeed()
    {
        Vector2 v = Vector2.Zero;
        for (int i = 0; i < 120; i++)
        {
            v = LocomotionMath.StepGround(v, Vector2.Up, _t.RunSpeed, _t, Dt);
        }

        Check.Near(_t.RunSpeed, v.Length(), 1e-3, "speed after 2s of run input");
    }

    [Test]
    public void NoInputBrakesToZero()
    {
        Vector2 v = new(0, -_t.SprintSpeed);
        for (int i = 0; i < 120; i++)
        {
            v = LocomotionMath.StepGround(v, Vector2.Zero, 0f, _t, Dt);
        }

        Check.Equal(Vector2.Zero, v, "velocity after 2s without input");
    }

    [Test]
    public void TurningIsTighterAtRunThanAtSprint()
    {
        Vector2 wish = new(0, 1);
        Vector2 atRun = LocomotionMath.StepGround(new Vector2(_t.RunSpeed, 0), wish, _t.RunSpeed, _t, Dt);
        Vector2 atSprint = LocomotionMath.StepGround(new Vector2(_t.SprintSpeed, 0), wish, _t.SprintSpeed, _t, Dt);
        float runTurn = Mathf.Abs(Vector2.Right.AngleTo(atRun));
        float sprintTurn = Mathf.Abs(Vector2.Right.AngleTo(atSprint));
        Check.True(runTurn > sprintTurn * 1.5f, $"run turn {runTurn:0.000} rad should exceed sprint turn {sprintTurn:0.000} rad");
    }

    [Test]
    public void SharpTurnsBleedSprintMomentumOnly()
    {
        Vector2 turned = LocomotionMath.StepGround(new Vector2(_t.SprintSpeed, 0), new Vector2(0, 1), _t.SprintSpeed, _t, Dt);
        Vector2 straight = LocomotionMath.StepGround(new Vector2(_t.SprintSpeed, 0), new Vector2(1, 0), _t.SprintSpeed, _t, Dt);
        Check.True(turned.Length() < straight.Length(), "turning at sprint must cost speed");
        Check.True(turned.Length() > _t.RunSpeed, "turn loss only applies to speed above run speed");
    }

    [Test]
    public void AirControlCannotExceedEntrySpeed()
    {
        Vector2 v = new(_t.SprintSpeed, 0);
        for (int i = 0; i < 60; i++)
        {
            v = LocomotionMath.StepAir(v, new Vector2(1, 0), 1f, _t, Dt);
        }

        Check.True(v.Length() <= _t.SprintSpeed + 1e-3f, $"air speed {v.Length():0.000} must not exceed entry speed");

        Vector2 slow = Vector2.Zero;
        for (int i = 0; i < 120; i++)
        {
            slow = LocomotionMath.StepAir(slow, new Vector2(1, 0), 1f, _t, Dt);
        }

        Check.Near(_t.AirControlMaxSpeed, slow.Length(), 1e-3, "air control from rest caps at AirControlMaxSpeed");
    }

    [Test]
    public void LandingClassificationTiers()
    {
        LandingType At(float height) => LocomotionMath.ClassifyLanding(_t.ImpactSpeedForFall(height), _t);
        Check.Equal(LandingType.Soft, At(_t.JumpHeight), "landing from own jump");
        Check.Equal(LandingType.Soft, At(3.19f), "3.19 m");
        Check.Equal(LandingType.Medium, At(3.21f), "3.21 m");
        Check.Equal(LandingType.Medium, At(5.11f), "5.11 m");
        Check.Equal(LandingType.Heavy, At(5.13f), "5.13 m");
        Check.Equal(LandingType.Heavy, At(6.40f), "6.40 m");
        Check.Equal(LandingType.Deadly, At(6.46f), "6.46 m");
    }

    [Test]
    public void TopSpeedDependsOnInputDirection()
    {
        Check.Near(_t.SprintSpeed, LocomotionMath.TopSpeedFor(1f, 1f, _t), 1e-4, "straight forward builds to sprint");
        Check.Near(_t.SprintSpeed, LocomotionMath.TopSpeedFor(0.7071f, 1f, _t), 1e-4, "forward diagonal still sprints");
        Check.Near(_t.StrafeMaxSpeed, LocomotionMath.TopSpeedFor(0f, 1f, _t), 1e-4, "pure strafe");
        Check.Near(_t.BackpedalMaxSpeed, LocomotionMath.TopSpeedFor(-1f, 1f, _t), 1e-4, "pure backpedal");
        Check.True(LocomotionMath.TopSpeedFor(1f, 0.3f, _t) <= _t.WalkSpeed, "light stick deflection walks");
        Check.Equal(0f, LocomotionMath.TopSpeedFor(1f, 0f, _t), "no input");
    }

    [Test]
    public void SpeedBandsAccelerateAtTheirOwnRate()
    {
        Check.Near(_t.WalkAcceleration * Dt, LocomotionMath.ApproachSpeed(0f, _t.SprintSpeed, _t, Dt), 1e-5, "walk band");
        float mid = (_t.WalkSpeed + _t.RunSpeed) * 0.5f;
        Check.Near(mid + _t.RunAcceleration * Dt, LocomotionMath.ApproachSpeed(mid, _t.SprintSpeed, _t, Dt), 1e-5, "run band");
        float high = (_t.RunSpeed + _t.SprintSpeed) * 0.5f;
        Check.Near(high + _t.SprintAcceleration * Dt, LocomotionMath.ApproachSpeed(high, _t.SprintSpeed, _t, Dt), 1e-5, "sprint band");
        Check.Near(_t.WalkSpeed, LocomotionMath.ApproachSpeed(_t.WalkSpeed - 0.01f, _t.SprintSpeed, _t, Dt), 1e-5, "never crosses a band boundary in one tick");
    }
}
