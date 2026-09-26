using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Veilrun.Player;
using Veilrun.Tests.Framework;

namespace Veilrun.Tests;

/// <summary>
/// Motor behaviour against real Jolt collision. These tests are also the source of the
/// measured values in docs/PARKOUR_METRICS.md (see METRIC lines in the runner output).
/// Convention: yaw 0 faces -Z, Move = (0, 1) is "forward".
/// </summary>
public sealed class PlayerMotorTests
{
    private static readonly Vector2 Forward = new(0, 1);
    private readonly MovementTuning _t = new();

    [Test]
    public async Task HoldingForwardProgressesWalkRunSprint(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 150f));
            Check.True(h.Settle(), "runner should settle on the floor");

            int toWalk = -1, toRun = -1, toSprint95 = -1;
            for (int i = 1; i <= 300; i++)
            {
                h.Step(Forward);
                float v = h.State.HorizontalSpeed;
                if (toWalk < 0 && v >= _t.WalkSpeed - 1e-3f) toWalk = i;
                if (toRun < 0 && v >= _t.RunSpeed - 1e-3f) toRun = i;
                if (toSprint95 < 0 && v >= 0.95f * _t.SprintSpeed) toSprint95 = i;
            }

            Check.Near(_t.SprintSpeed, h.State.HorizontalSpeed, 0.01, "holding forward ends at sprint speed");
            Check.Equal(LocomotionMode.Sprint, h.State.Mode, "mode after 5 s of forward");
            ctx.Metric("time_to_walk_speed", toWalk * h.Dt, "s");
            ctx.Metric("time_to_run_speed", toRun * h.Dt, "s");
            ctx.Metric("time_to_95pct_sprint_speed", toSprint95 * h.Dt, "s");
            Check.True(toWalk < toRun && toRun < toSprint95, "bands are reached in order");
            Check.InRange(toWalk * h.Dt, 0.03, 0.15, "first step must be immediate");
            Check.InRange(toRun * h.Dt, 0.2, 0.6, "run arrives quickly");
            Check.InRange(toSprint95 * h.Dt, 1.0, 2.5, "sprint is earned");
        });
    }

    [Test]
    public async Task StrafeAndBackpedalNeverSprint(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            foreach ((Vector2 move, float expected, string name) in new[]
                     {
                         (new Vector2(1, 0), _t.StrafeMaxSpeed, "strafe"),
                         (new Vector2(0, -1), _t.BackpedalMaxSpeed, "backpedal"),
                     })
            {
                var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 0));
                h.Settle();
                for (int i = 0; i < 240; i++)
                {
                    h.Step(move);
                }

                Check.Near(expected, h.State.HorizontalSpeed, 0.01, name + " top speed");
                h.Dispose();
            }
        });
    }

    [Test]
    public async Task SprintMomentumSurvivesAJumpButNotASharpTurn(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 150f));
            h.Settle();
            for (int i = 0; i < 240; i++)
            {
                h.Step(Forward);
            }

            h.Step(Forward, 0f, InputButtons.Jump);
            while (!h.State.IsGrounded)
            {
                h.Step(Forward);
            }

            float landed = h.State.HorizontalSpeed;
            Check.True(landed > 0.97f * _t.SprintSpeed, "soft landing keeps sprint momentum (" + landed.ToString("0.00") + " m/s)");

            // Snap the view 90 degrees right while pushing forward: the line breaks, momentum bleeds.
            float lowest = float.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                h.Step(Forward, -Mathf.Pi / 2f);
                lowest = Mathf.Min(lowest, h.State.HorizontalSpeed);
            }

            ctx.Metric("lowest_speed_in_90deg_turn_at_sprint", lowest, "m/s");
            Check.True(lowest < _t.SprintSpeed - 0.5f, "a 90 degree turn must cost sprint speed");
            Check.True(lowest > _t.RunSpeed - 0.01f, "turning never drops below run speed");
        });
    }

    [Test]
    public async Task JumpApexMatchesTuning(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 0));
            h.Settle();
            float startY = h.State.Position.Y;
            float apex = startY;
            int airTicks = 0;
            MotorEvents e = h.Step(Vector2.Zero, 0f, InputButtons.Jump);
            Check.True(e.Jumped, "jump should trigger from the ground");
            for (int i = 0; i < 180 && !h.State.IsGrounded; i++)
            {
                apex = Mathf.Max(apex, h.State.Position.Y);
                airTicks++;
                h.Step(Vector2.Zero);
            }

            float height = apex - startY;
            ctx.Metric("standing_jump_apex", height, "m");
            ctx.Metric("standing_jump_airtime", airTicks * h.Dt, "s");
            Check.Near(_t.JumpHeight, height, 0.03, "jump apex");
        });
    }

    [Test]
    public async Task RunningAndSprintingJumpDistances(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            float expectedAirtime = _t.JumpTimeToApex + Mathf.Sqrt(2f * _t.JumpHeight / _t.FallGravity);

            foreach ((string name, float takeoffSpeed) in new[]
                     {
                         ("walking_jump_distance", _t.WalkSpeed),
                         ("running_jump_distance", _t.RunSpeed),
                         ("sprint_jump_distance", _t.SprintSpeed),
                     })
            {
                var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 150f));
                h.Settle();
                int guard = 0;
                while (h.State.HorizontalSpeed < takeoffSpeed - 1e-3f && guard++ < 600)
                {
                    h.Step(Forward);
                }

                float speed = h.State.HorizontalSpeed;
                Vector3 takeoff = h.State.Position;
                h.Step(Forward, 0f, InputButtons.Jump);
                guard = 0;
                while (!h.State.IsGrounded && guard++ < 240)
                {
                    h.Step(Forward);
                }

                Vector3 landing = h.State.Position;
                float distance = new Vector2(landing.X - takeoff.X, landing.Z - takeoff.Z).Length();
                ctx.Metric(name, distance, "m");
                Check.Near(speed * expectedAirtime, distance, 0.25, name);
                h.Dispose();
            }
        });
    }

    [Test]
    public async Task FootstepsFollowStrideAndStopInTheAir(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 150f));
            h.Settle();
            for (int i = 0; i < 240; i++)
            {
                h.Step(Forward);
            }

            int steps = 0;
            Vector3 start = h.State.Position;
            for (int i = 0; i < 120; i++)
            {
                if (h.Step(Forward).Footstep) steps++;
            }

            float distance = start.DistanceTo(h.State.Position);
            float expected = distance / _t.StepLengthAt(_t.SprintSpeed);
            ctx.Metric("sprint_cadence", steps / 2f, "steps/s");
            Check.Near(expected, steps, 1.01, "footsteps at sprint follow stride length");

            int airSteps = 0;
            h.Step(Forward, 0f, InputButtons.Jump);
            while (!h.State.IsGrounded)
            {
                if (h.Step(Forward).Footstep) airSteps++;
            }

            Check.Equal(0, airSteps, "no footsteps while airborne");
        });
    }

    [Test]
    public async Task CoyoteTimeAllowsLateJumpOnly(TestContext ctx)
    {
        // Ledge: a platform whose top is at y=3 ending at z=-5; ground far below.
        TestWorld.Floor(ctx, 0f);
        TestWorld.Box(ctx, new Vector3(0, 1.5f, 5f), new Vector3(6, 3, 20));
        await ctx.PhysicsFrame();

        bool JumpAfterLeaving(int lateTicks)
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 3.05f, 10f));
            h.Settle();
            bool left = false;
            int sinceLeft = 0;
            for (int i = 0; i < 300; i++)
            {
                bool press = left && sinceLeft == lateTicks;
                MotorEvents e = h.Step(Forward, 0f, press ? InputButtons.Jump : InputButtons.None);
                if (e.Jumped)
                {
                    h.Dispose();
                    return true;
                }

                if (e.LeftGround)
                {
                    left = true;
                }

                if (left && ++sinceLeft > lateTicks + 1)
                {
                    break;
                }
            }

            h.Dispose();
            return false;
        }

        await ctx.InPhysicsFrame(() =>
        {
            int insideTicks = Mathf.FloorToInt(_t.CoyoteTime / (1f / Engine.PhysicsTicksPerSecond)) - 1;
            int outsideTicks = Mathf.CeilToInt(_t.CoyoteTime / (1f / Engine.PhysicsTicksPerSecond)) + 2;
            Check.True(JumpAfterLeaving(insideTicks), $"jump {insideTicks} ticks after leaving the ledge should use coyote time");
            Check.False(JumpAfterLeaving(outsideTicks), $"jump {outsideTicks} ticks after leaving the ledge must be rejected");
        });
    }

    [Test]
    public async Task JumpBufferFiresOnLanding(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();

        // Returns the tick (relative to drop start) at which a jump fired, or -1.
        int DropAndPress(int pressTick, out int landingTick)
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 3f, 0));
            landingTick = -1;
            int jumpedAt = -1;
            for (int i = 0; i < 120; i++)
            {
                MotorEvents e = h.Step(Vector2.Zero, 0f, i == pressTick ? InputButtons.Jump : InputButtons.None);
                if (e.Landed is not null && landingTick < 0)
                {
                    landingTick = i;
                }

                if (e.Jumped)
                {
                    jumpedAt = i;
                    break;
                }
            }

            h.Dispose();
            return jumpedAt;
        }

        await ctx.InPhysicsFrame(() =>
        {
            DropAndPress(-1, out int landing);
            Check.True(landing > 0, "dry run must land");

            int early = Mathf.FloorToInt(_t.JumpBufferTime * Engine.PhysicsTicksPerSecond) - 2;
            int jumped = DropAndPress(landing - early, out _);
            Check.InRange(jumped, landing, landing + 1, $"jump pressed {early} ticks before landing should fire on landing");

            int tooEarly = Mathf.CeilToInt(_t.JumpBufferTime * Engine.PhysicsTicksPerSecond) + 3;
            Check.Equal(-1, DropAndPress(landing - tooEarly, out _), $"jump pressed {tooEarly} ticks before landing must expire");
        });
    }

    [Test]
    public async Task FallHeightsClassifyLandings(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var cases = new List<(float height, LandingType expected)>
            {
                (2f, LandingType.Soft),
                (4f, LandingType.Medium),
                (5.8f, LandingType.Heavy),
                (8f, LandingType.Deadly),
            };
            foreach ((float height, LandingType expected) in cases)
            {
                var h = MotorHarness.Create(ctx, _t, new Vector3(0, height, 0));
                LandingEvent? landed = null;
                for (int i = 0; i < 300 && landed is null; i++)
                {
                    landed = h.Step(Vector2.Zero).Landed;
                }

                Check.True(landed is not null, $"drop from {height} m should land");
                Check.Equal(expected, landed!.Value.Type, $"landing type from {height} m");
                h.Dispose();
            }

            ctx.Metric("medium_landing_impact_speed", _t.MediumLandingSpeed, "m/s");
            ctx.Metric("heavy_landing_impact_speed", _t.HeavyLandingSpeed, "m/s");
            ctx.Metric("deadly_landing_impact_speed", _t.DeadlyLandingSpeed, "m/s");
        });
    }

    [Test]
    public async Task WallStopsRunnerWithoutPenetration(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 2f, -10.5f), new Vector3(10, 4, 1)); // wall face at z = -10
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 0));
            h.Settle();
            for (int i = 0; i < 180; i++)
            {
                h.Step(Forward);
            }

            float frontOfCapsule = h.State.Position.Z - _t.CapsuleRadius;
            Check.True(frontOfCapsule >= -10.01f, $"capsule front {frontOfCapsule:0.000} must not pass wall face at -10");
            Check.True(h.State.HorizontalSpeed < 0.5f, "runner should be stopped by the wall");
        });
    }

    [Test]
    public async Task WalkableAndUnwalkableSlopes(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        // Ramps rise toward -Z. A box rotated around X by +angle tilts its top face up toward -Z.
        TestWorld.Box(ctx, new Vector3(-10, 0, -10), new Vector3(4, 0.5f, 30), new Vector3(30, 0, 0));
        TestWorld.Box(ctx, new Vector3(10, 0, -10), new Vector3(4, 0.5f, 30), new Vector3(55, 0, 0));
        await ctx.PhysicsFrame();

        float Climb(float x)
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(x, 0.05f, 2f));
            h.Settle();
            float maxY = 0f;
            for (int i = 0; i < 240; i++)
            {
                h.Step(Forward);
                maxY = Mathf.Max(maxY, h.State.Position.Y);
            }

            h.Dispose();
            return maxY;
        }

        await ctx.InPhysicsFrame(() =>
        {
            float gentle = Climb(-10f);
            float steep = Climb(10f);
            ctx.Metric("height_gained_on_30deg_ramp_4s", gentle, "m");
            ctx.Metric("height_gained_on_55deg_ramp_4s", steep, "m");
            Check.True(gentle > 3f, $"30° ramp must be walkable (gained {gentle:0.00} m)");
            Check.True(steep < 1.0f, $"55° ramp must not be walkable (gained {steep:0.00} m)");
        });
    }

    [Test]
    public async Task MeasureMaxClimbableStep(TestContext ctx)
    {
        // Measures how tall a vertical step the capsule rides over without any step-up logic.
        float[] heights = { 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.4f, 0.5f };
        TestWorld.Floor(ctx);
        for (int i = 0; i < heights.Length; i++)
        {
            TestWorld.Box(ctx, new Vector3(i * 4f, heights[i] * 0.5f, -10f), new Vector3(3f, heights[i], 6f));
        }

        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            float maxClimbed = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                var h = MotorHarness.Create(ctx, _t, new Vector3(i * 4f, 0.05f, 0f));
                h.Settle();
                float peakY = 0f;
                for (int t = 0; t < 150; t++)
                {
                    h.Step(Forward);
                    if (h.State.IsGrounded)
                    {
                        peakY = Mathf.Max(peakY, h.State.Position.Y);
                    }
                }

                bool climbed = peakY > heights[i] - 0.02f;
                if (climbed)
                {
                    maxClimbed = Mathf.Max(maxClimbed, heights[i]);
                }

                h.Dispose();
            }

            ctx.Metric("max_climbable_step_without_stepup", maxClimbed, "m");
            Check.True(maxClimbed < 0.5f, "a 0.5 m block must require a jump (or mantle in M2)");
        });
    }

    [Test]
    public async Task ReplayFromSnapshotIsDeterministic(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(3, 0.5f, -12), new Vector3(4, 1, 4));
        TestWorld.Box(ctx, new Vector3(-4, 2f, -20), new Vector3(2, 4, 6));
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = MotorHarness.Create(ctx, _t, new Vector3(0, 0.05f, 0));
            h.Settle();

            var script = new List<InputCommand>();
            for (int i = 0; i < 360; i++)
            {
                float yaw = Mathf.Sin(i * 0.02f) * 0.8f;
                var buttons = i % 70 == 35 ? InputButtons.Jump : InputButtons.None;
                Vector2 move = i % 150 < 120 ? Forward : new Vector2(0.7f, 0.7f);
                script.Add(h.Command(move, yaw, buttons));
            }

            // Reference run, snapshotting the state before every tick (a prediction history buffer).
            var history = new List<MotorState>(script.Count);
            foreach (InputCommand cmd in script)
            {
                history.Add(h.Motor.CaptureState());
                h.Step(cmd);
            }

            MotorState reference = h.Motor.CaptureState();
            Check.True(reference.Position.DistanceTo(history[0].Position) > 5f, "sanity: the script actually moved the runner");

            // Rollback to many checkpoints — grounded and mid-air — and replay to the end.
            // This is the reconciliation contract: same snapshot + same commands = same result.
            int airborneCheckpoints = 0;
            float worstError = 0f;
            for (int start = 0; start < script.Count; start += 7)
            {
                if (!history[start].IsGrounded)
                {
                    airborneCheckpoints++;
                }

                // Worst case for rollback: the body's internal floor flag disagrees with the snapshot.
                ForceBodyFloorFlag(h.Body, !history[start].IsGrounded);
                h.Motor.RestoreState(history[start]);
                for (int i = start; i < script.Count; i++)
                {
                    h.Step(script[i]);
                }

                MotorState replay = h.Motor.CaptureState();
                float error = reference.Position.DistanceTo(replay.Position);
                worstError = Mathf.Max(worstError, error);
                Check.True(error < 1e-4f, $"replay from tick {start} ({(history[start].IsGrounded ? "grounded" : "airborne")}) diverged by {error} m");
                Check.True(reference.Velocity.DistanceTo(replay.Velocity) < 1e-4f, $"replay from tick {start} diverged in velocity");
                Check.Equal(reference.IsGrounded, replay.IsGrounded, $"replay from tick {start} grounded state");
            }

            ctx.Metric("replay_worst_position_error", worstError, "m");
            Check.True(airborneCheckpoints >= 3, $"sanity: need airborne checkpoints, got {airborneCheckpoints}");
        });
    }

    /// <summary>Put CharacterBody3D's private on-floor flag into a known state by moving it somewhere neutral.</summary>
    private static void ForceBodyFloorFlag(CharacterBody3D body, bool onFloor)
    {
        body.GlobalPosition = onFloor ? new Vector3(80, 0.02f, 80) : new Vector3(80, 30f, 80);
        body.Velocity = onFloor ? Vector3.Down : Vector3.Up;
        body.MoveAndSlide();
        Check.Equal(onFloor, body.IsOnFloor(), "harness failed to force floor flag");
    }
}
