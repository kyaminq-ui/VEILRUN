using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Veilrun.Player;
using Veilrun.Tests.Framework;
using Veilrun.Traversal;

namespace Veilrun.Tests;

/// <summary>
/// M2 parkour moves against real Jolt collision. Convention: yaw 0 faces -Z, runners start
/// at +Z and run toward obstacles placed around z = -30 (enough runway to reach sprint).
/// </summary>
public sealed class TraversalTests
{
    private static readonly Vector2 Forward = new(0, 1);
    private readonly MovementTuning _m = new();
    private readonly TraversalTuning _t = new();

    private sealed class Trace
    {
        public readonly HashSet<TraversalKind> Started = new();
        public readonly HashSet<TraversalKind> Ended = new();
        public float MaxY = float.MinValue;
        public int Ticks;
        public MotorEvents Last;
    }

    /// <summary>Step until <paramref name="stop"/> is true or maxTicks, recording which moves started.</summary>
    private static Trace Run(MotorHarness h, Func<int, (Vector2 move, InputButtons buttons)> input, Func<MotorHarness, MotorEvents, bool> stop, int maxTicks = 900)
    {
        var trace = new Trace();
        for (int i = 0; i < maxTicks; i++)
        {
            (Vector2 move, InputButtons buttons) = input(i);
            MotorEvents e = h.Step(move, 0f, buttons);
            trace.Ticks++;
            trace.Last = e;
            if (e.Started != TraversalKind.None) trace.Started.Add(e.Started);
            if (e.Ended != TraversalKind.None) trace.Ended.Add(e.Ended);
            trace.MaxY = Mathf.Max(trace.MaxY, h.State.Position.Y);
            if (stop(h, e))
            {
                break;
            }
        }

        return trace;
    }

    private MotorHarness Spawn(TestContext ctx, float x = 0f, float z = 10f)
    {
        var h = MotorHarness.Create(ctx, _m, new Vector3(x, 0.05f, z), _t);
        h.Settle();
        return h;
    }

    // ------------------------------------------------------------------ vault / mantle

    [Test]
    public async Task SprintVaultOverRailKeepsMomentum(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 0.5f, -30f), new Vector3(6f, 1.0f, 0.2f)); // 1.0 m rail, 0.2 m thick
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx);
            float entry = 0f, exit = 0f, previous = 0f;
            int vaultTicks = 0;
            Run(h, _ => (Forward, InputButtons.None), (hh, e) =>
            {
                if (e.Started == TraversalKind.Vault) entry = previous;
                if (hh.State.Traversal == TraversalKind.Vault) vaultTicks++;
                if (e.Ended == TraversalKind.Vault) exit = hh.State.HorizontalSpeed;
                previous = hh.State.HorizontalSpeed;
                return hh.State.Position.Z < -34f;
            });

            Check.True(entry > 0f, "the rail must be vaulted");
            Check.True(h.State.Position.Z < -34f, "runner must end past the rail");
            ctx.Metric("vault_1m_rail_duration", (vaultTicks + 1) * h.Dt, "s");
            ctx.Metric("vault_speed_retention", exit / entry, "ratio");
            Check.True(exit >= 0.9f * entry, $"vault keeps momentum ({entry:0.00} → {exit:0.00} m/s)");
        });
    }

    [Test]
    public async Task MantleOntoHighBlockAndQuickClimbLowBlock(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 0.8f, -30f), new Vector3(6f, 1.6f, 4f));    // 1.6 m — mantle
        TestWorld.Box(ctx, new Vector3(10, 0.45f, -30f), new Vector3(6f, 0.9f, 6f));  // 0.9 m — quick climb
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var high = Spawn(ctx);
            int ticks = 0;
            Trace t1 = Run(high, _ => (Forward, InputButtons.None), (hh, e) =>
            {
                if (hh.State.Traversal == TraversalKind.Mantle) ticks++;
                return e.Ended == TraversalKind.Mantle;
            });
            Check.True(t1.Started.Contains(TraversalKind.Mantle), "1.6 m block must be mantled");
            Check.True(high.State.IsGrounded && Mathf.Abs(high.State.Position.Y - 1.6f) < 0.06f, $"on top after mantle (y={high.State.Position.Y:0.00})");
            ctx.Metric("mantle_1_6m_duration", (ticks + 1) * high.Dt, "s");
            high.Dispose();

            var low = Spawn(ctx, x: 10f);
            float entry = 0f, previous = 0f;
            Trace t2 = Run(low, _ => (Forward, InputButtons.None), (hh, e) =>
            {
                if (e.Started == TraversalKind.Mantle) entry = previous;
                previous = hh.State.HorizontalSpeed;
                return e.Ended == TraversalKind.Mantle;
            });
            Check.True(t2.Started.Contains(TraversalKind.Mantle), "0.9 m block must be quick-climbed");
            float kept = low.State.HorizontalSpeed / entry;
            ctx.Metric("quick_climb_0_9m_speed_retention", kept, "ratio");
            Check.True(kept >= 0.8f, $"quick climb keeps most of the momentum ({kept:0.00})");
        });
    }

    [Test]
    public async Task GroundMantleLimitIsTwoMeters(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 1.0f, -30f), new Vector3(6f, 2.0f, 4f));   // 2.0 m: climbable from the ground
        TestWorld.Box(ctx, new Vector3(10, 1.2f, -30f), new Vector3(6f, 2.4f, 4f));  // 2.4 m: needs a jump
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            // Walk in slowly (analog-light forward is still ≥ MinForwardInput) so no wall climb happens.
            var twoMeters = Spawn(ctx, z: -26f);
            Trace a = Run(twoMeters, _ => (Forward, InputButtons.None), (hh, e) => e.Ended == TraversalKind.Mantle, 240);
            Check.True(a.Started.Contains(TraversalKind.Mantle), "2.0 m ledge is mantled from the ground");
            twoMeters.Dispose();

            var tooHigh = Spawn(ctx, x: 10f, z: -26f);
            Trace b = Run(tooHigh, _ => (Forward, InputButtons.None), (hh, e) => false, 180);
            Check.False(b.Started.Contains(TraversalKind.Mantle), "2.4 m ledge cannot be mantled without jumping");
            Check.True(tooHigh.State.Position.Y < 0.1f, "runner stays on the ground in front of a 2.4 m wall");
        });
    }

    // ------------------------------------------------------------------ ledge

    [Test]
    public async Task JumpToLedgeHangShimmyDropAndClimb(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 1.5f, -30f), new Vector3(8f, 3.0f, 4f)); // 3.0 m wall, face at z = -28
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            // Walk-run in and jump 1.2 m before the face (too far for a wall climb).
            var h = Spawn(ctx, z: -20f);
            bool jumped = false;
            Trace grab = Run(h, _ =>
            {
                bool jump = !jumped && h.State.Position.Z <= -26.45f;
                jumped |= jump;
                return (Forward, jump ? InputButtons.Jump : InputButtons.None);
            }, (hh, e) => e.Started == TraversalKind.LedgeHang, 400);
            Check.True(grab.Started.Contains(TraversalKind.LedgeHang), "hands must catch the 3.0 m ledge");

            // Hang still (no input): stable.
            Run(h, _ => (Vector2.Zero, InputButtons.None), (_, _) => false, 30);
            Vector3 hang = h.State.Position;
            Check.Equal(TraversalKind.LedgeHang, h.State.Traversal, "still hanging without input");
            ctx.Metric("ledge_hang_feet_below_top", 3.0f - hang.Y, "m");

            // Shimmy right for 0.5 s.
            Run(h, _ => (new Vector2(1, 0), InputButtons.None), (_, _) => false, 30);
            float shimmied = h.State.Position.X - hang.X;
            ctx.Metric("shimmy_distance_0_5s", shimmied, "m");
            Check.Near(_t.ShimmySpeed * 0.5f, shimmied, 0.1, "shimmy along the ledge");

            // Climb up by pushing forward.
            Trace climb = Run(h, _ => (Forward, InputButtons.None), (hh, e) => e.Ended == TraversalKind.LedgeClimb, 120);
            Check.True(climb.Started.Contains(TraversalKind.LedgeClimb), "forward climbs the ledge");
            Check.True(h.State.IsGrounded && Mathf.Abs(h.State.Position.Y - 3.0f) < 0.06f, $"on top after ledge climb (y={h.State.Position.Y:0.00})");
            h.Dispose();

            // Separate runner: grab, then drop with crouch.
            var d = Spawn(ctx, x: 2f, z: -20f);
            jumped = false;
            Run(d, _ =>
            {
                bool jump = !jumped && d.State.Position.Z <= -26.45f;
                jumped |= jump;
                return (Forward, jump ? InputButtons.Jump : InputButtons.None);
            }, (hh, e) => e.Started == TraversalKind.LedgeHang, 400);
            Run(d, _ => (Vector2.Zero, InputButtons.None), (_, _) => false, 10);
            Trace drop = Run(d, i => (Vector2.Zero, i == 0 ? InputButtons.Crouch : InputButtons.None), (hh, e) => e.Landed is not null, 120);
            Check.True(drop.Ended.Contains(TraversalKind.LedgeHang) && d.State.IsGrounded, "crouch drops from the ledge to the ground");
        });
    }

    // ------------------------------------------------------------------ wall run / wall climb

    [Test]
    public async Task WallRunCarriesFurtherThanASprintJump(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(-1.5f, 2.5f, -45f), new Vector3(1f, 5f, 50f)); // wall face at x = -1, z -20..-70
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx, x: -0.45f);
            bool jumped = false;
            float startZ = 0f;
            float wallRunTime = 0f;
            Trace t = Run(h, _ =>
            {
                bool jump = !jumped && h.State.Position.Z <= -24f;
                jumped |= jump;
                return (Forward, jump ? InputButtons.Jump : InputButtons.None);
            }, (hh, e) =>
            {
                if (e.Started == TraversalKind.WallRun) startZ = hh.State.Position.Z;
                if (hh.State.Traversal == TraversalKind.WallRun) wallRunTime += hh.Dt;
                return jumped && e.Landed is not null;
            });

            Check.True(t.Started.Contains(TraversalKind.WallRun), "jumping along the wall starts a wall run");
            float distance = startZ - h.State.Position.Z;
            ctx.Metric("wall_run_time", wallRunTime, "s");
            ctx.Metric("wall_run_jump_to_landing_distance", distance, "m");
            ctx.Metric("wall_run_max_feet_height", t.MaxY, "m");
            Check.True(distance > 8f, $"wall run must carry further than a sprint jump ({distance:0.00} m)");
        });
    }

    [Test]
    public async Task WallJumpPushesAwayAndBlocksRerunningTheSameWall(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(-1.5f, 2.5f, -45f), new Vector3(1f, 5f, 50f));
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx, x: -0.45f);
            bool jumped = false;
            int wallTicks = 0;
            Trace t = Run(h, _ =>
            {
                bool jump = (!jumped && h.State.Position.Z <= -24f) || wallTicks == 18;
                jumped |= jump;
                return (Forward, jump ? InputButtons.Jump : InputButtons.None);
            }, (hh, e) =>
            {
                if (hh.State.Traversal == TraversalKind.WallRun) wallTicks++;
                return e.WallJumped;
            });

            Check.True(t.Last.WallJumped, "jump during a wall run is a wall jump");
            Check.True(h.State.Velocity.X > 3f, $"wall jump pushes away from the wall (vx={h.State.Velocity.X:0.00})");
            Check.True(h.State.Velocity.Y > 3f, "wall jump goes up");
            Check.True(h.State.WallCooldown > 0f, "same-wall cooldown armed");
        });
    }

    [Test]
    public async Task WallClimbReachesLedgesAboveJumpHeight(TestContext ctx)
    {
        float[] heights = { 3.0f, 3.25f, 3.5f, 3.75f, 4.0f, 4.25f };
        TestWorld.Floor(ctx);
        for (int i = 0; i < heights.Length; i++)
        {
            TestWorld.Box(ctx, new Vector3(i * 8f, heights[i] * 0.5f, -30f), new Vector3(6f, heights[i], 4f));
        }

        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            float best = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                var h = Spawn(ctx, x: i * 8f);
                bool jumped = false;
                Trace t = Run(h, _ =>
                {
                    bool jump = !jumped && h.State.Position.Z <= -27.5f;
                    jumped |= jump;
                    return (Forward, jump ? InputButtons.Jump : InputButtons.None);
                }, (hh, e) => (hh.State.IsGrounded && hh.State.Position.Y > 2.5f) || (jumped && e.Landed is not null), 600);

                Check.True(t.Started.Contains(TraversalKind.WallClimb), $"jump into the {heights[i]} m wall must run up it");
                if (h.State.IsGrounded && Mathf.Abs(h.State.Position.Y - heights[i]) < 0.06f)
                {
                    best = heights[i];
                }

                h.Dispose();
            }

            ctx.Metric("max_wall_height_via_wall_climb", best, "m");
            Check.True(best >= 3.5f, $"wall climb + ledge must reach at least 3.5 m (got {best} m)");
        });
    }

    [Test]
    public async Task WallKickTurnsAroundAndPushesAway(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 3f, -30f), new Vector3(6f, 6f, 4f)); // 6 m: no ledge in reach
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx);
            bool jumped = false;
            int climbTicks = 0;
            Trace t = Run(h, _ =>
            {
                bool jump = (!jumped && h.State.Position.Z <= -27.5f) || climbTicks == 12;
                jumped |= jump;
                return (Forward, jump ? InputButtons.Jump : InputButtons.None);
            }, (hh, e) =>
            {
                if (hh.State.Traversal == TraversalKind.WallClimb) climbTicks++;
                return e.TurnAround;
            }, 600);

            Check.True(t.Last.TurnAround, "jump during a wall climb is a wall kick");
            Check.True(h.State.Velocity.Z > 3f, "kick pushes away from the wall (+Z)");
        });
    }

    // ------------------------------------------------------------------ slide / crouch / roll

    [Test]
    public async Task SlideUnderLowBarAndStandUpAfter(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 1.25f, -30f), new Vector3(6f, 0.5f, 2f)); // bar bottom at 1.0 m
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx);
            float slideStartZ = 0f, slideEndZ = 0f;
            Trace t = Run(h, _ =>
            {
                float z = h.State.Position.Z;
                bool crouch = z <= -24f && z > -32f;
                return (Forward, crouch ? InputButtons.Crouch : InputButtons.None);
            }, (hh, e) =>
            {
                if (e.Started == TraversalKind.Slide) slideStartZ = hh.State.Position.Z;
                if (e.Ended == TraversalKind.Slide) slideEndZ = hh.State.Position.Z;
                return hh.State.Position.Z < -36f;
            });

            Check.True(t.Started.Contains(TraversalKind.Slide), "crouch at sprint starts a slide");
            Check.True(h.State.Position.Z < -36f, "runner must get past the 1.0 m bar");
            Check.False(h.State.IsCrouched, "runner stands up once clear of the bar");
            ctx.Metric("slide_distance_with_exit", slideStartZ - slideEndZ, "m");
        });
    }

    [Test]
    public async Task FlatSlideDistanceAndSlideJump(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx, z: 150f);
            Run(h, _ => (Forward, InputButtons.None), (_, _) => false, 200);
            float startZ = h.State.Position.Z;
            Trace slide = Run(h, _ => (Forward, InputButtons.Crouch), (hh, e) => e.Ended == TraversalKind.Slide, 300);
            Check.True(slide.Started.Contains(TraversalKind.Slide), "slide starts");
            ctx.Metric("flat_slide_distance", startZ - h.State.Position.Z, "m");
            ctx.Metric("flat_slide_duration", slide.Ticks * h.Dt, "s");
            h.Dispose();

            var j = Spawn(ctx, x: 5f, z: 150f);
            Run(j, _ => (Forward, InputButtons.None), (_, _) => false, 200);
            Run(j, _ => (Forward, InputButtons.Crouch), (_, _) => false, 8);
            float speed = j.State.HorizontalSpeed;
            float takeoffZ = j.State.Position.Z;
            Trace jump = Run(j, i => (Forward, i == 0 ? InputButtons.Jump | InputButtons.Crouch : InputButtons.None), (hh, e) => e.Landed is not null, 200);
            Check.True(speed > _m.SprintSpeed, $"slide carries more than sprint speed ({speed:0.00} m/s)");
            ctx.Metric("slide_jump_distance", takeoffZ - j.State.Position.Z, "m");
            Check.True(takeoffZ - j.State.Position.Z > 5.5f, "slide jump beats a plain sprint jump");
        });
    }

    [Test]
    public async Task CrouchWalkUnderLowCeiling(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 1.65f, -10f), new Vector3(6f, 0.5f, 6f)); // ceiling bottom at 1.4 m, z -7..-13
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx, z: 0f);
            // Crouch the whole way in, release under the ceiling: must stay crouched until clear.
            bool crouchedUnder = false;
            Run(h, _ => (Forward, h.State.Position.Z > -9f ? InputButtons.Crouch : InputButtons.None), (hh, e) =>
            {
                float z = hh.State.Position.Z;
                if (z < -8f && z > -12f && hh.State.IsCrouched) crouchedUnder = true;
                return z < -15f;
            }, 600);

            Check.True(crouchedUnder, "runner stays crouched under the low ceiling after releasing crouch");
            Check.True(h.State.Position.Z < -15f, "crouch-walk passes under a 1.4 m ceiling");
            Check.False(h.State.IsCrouched, "stands up once clear");
        });
    }

    [Test]
    public async Task LandingRollSavesMediumAndHeavyButNotDeadly(TestContext ctx)
    {
        // Sprint off the end of platforms of different heights (edge at z = -50) onto the floor.
        float[] heights = { 4.2f, 5.8f, 8f };
        TestWorld.Floor(ctx);
        for (int i = 0; i < heights.Length; i++)
        {
            TestWorld.Box(ctx, new Vector3(i * 6f, heights[i] * 0.5f, -20f), new Vector3(3f, heights[i], 60f));
        }

        await ctx.PhysicsFrame();

        (LandingEvent landing, float speedAfter) RunOff(int lane, bool roll)
        {
            float x = lane * 6f, top = heights[lane];
            int landingTick = -1;
            var dry = MotorHarness.Create(ctx, _m, new Vector3(x, top + 0.05f, 8f), _t);
            for (int i = 0; i < 900 && landingTick < 0; i++)
            {
                if (dry.Step(Forward).Landed is { } l && dry.State.Position.Y < 0.5f) landingTick = i;
            }

            dry.Dispose();
            var h = MotorHarness.Create(ctx, _m, new Vector3(x, top + 0.05f, 8f), _t);
            LandingEvent? landed = null;
            for (int i = 0; i < 900 && landed is null; i++)
            {
                InputButtons b = roll && i == landingTick - 10 ? InputButtons.Crouch : InputButtons.None;
                if (h.Step(Forward, 0f, b).Landed is { } l && h.State.Position.Y < 0.5f) landed = l;
            }

            float speed = h.State.HorizontalSpeed;
            h.Dispose();
            return (landed!.Value, speed);
        }

        await ctx.InPhysicsFrame(() =>
        {
            var medium = RunOff(0, roll: true);
            var mediumNoRoll = RunOff(0, roll: false);
            Check.Equal(LandingType.Medium, medium.landing.Type, "4.2 m drop is Medium");
            Check.True(medium.landing.Rolled, "crouch before landing rolls");
            Check.True(medium.speedAfter > mediumNoRoll.speedAfter + 0.5f,
                $"roll keeps momentum ({medium.speedAfter:0.00} vs {mediumNoRoll.speedAfter:0.00} m/s)");

            var heavy = RunOff(1, roll: true);
            var heavyNoRoll = RunOff(1, roll: false);
            Check.Equal(LandingType.Heavy, heavy.landing.Type, "5.8 m drop is Heavy");
            Check.True(heavy.landing.Rolled, "Heavy landings can be rolled");
            ctx.Metric("speed_after_heavy_landing_roll", heavy.speedAfter, "m/s");
            ctx.Metric("speed_after_heavy_landing_no_roll", heavyNoRoll.speedAfter, "m/s");

            var deadly = RunOff(2, roll: true);
            Check.Equal(LandingType.Deadly, deadly.landing.Type, "8 m drop is Deadly");
            Check.False(deadly.landing.Rolled, "a roll never saves a Deadly fall");
        });
    }

    // ------------------------------------------------------------------ budget & determinism

    [Test]
    public async Task ParkourCourseIsDeterministicAndWithinProbeBudget(TestContext ctx)
    {
        TestWorld.Floor(ctx);
        TestWorld.Box(ctx, new Vector3(0, 0.5f, -30f), new Vector3(8f, 1.0f, 0.2f));   // rail → vault
        TestWorld.Box(ctx, new Vector3(0, 1.25f, -45f), new Vector3(8f, 0.5f, 2f));    // bar → slide
        TestWorld.Box(ctx, new Vector3(0, 0.8f, -62f), new Vector3(8f, 1.6f, 6f));     // block → mantle
        TestWorld.Box(ctx, new Vector3(-4.5f, 2.5f, -90f), new Vector3(1f, 5f, 30f));  // wall → wall run
        await ctx.PhysicsFrame();
        await ctx.InPhysicsFrame(() =>
        {
            var h = Spawn(ctx);
            h.Motor.Probes.ResetPeak();
            var script = new List<InputCommand>();
            var history = new List<MotorState>();
            var seen = new HashSet<TraversalKind>();
            for (int i = 0; i < 900; i++)
            {
                float z = h.State.Position.Z;
                InputButtons b = InputButtons.None;
                if (z <= -39f && z > -47f) b |= InputButtons.Crouch;
                if (i % 97 == 60 && z < -66f) b |= InputButtons.Jump;
                Vector2 move = z < -66f ? new Vector2(-0.45f, 0.9f) : Forward; // veer toward the wall after the block
                InputCommand cmd = h.Command(move, 0f, b);
                history.Add(h.Motor.CaptureState());
                script.Add(cmd);
                MotorEvents e = h.Step(cmd);
                if (e.Started != TraversalKind.None) seen.Add(e.Started);
            }

            MotorState reference = h.Motor.CaptureState();
            ctx.Metric("course_moves_seen", seen.Count, "kinds");
            ctx.Metric("probe_peak_queries_per_tick", h.Motor.Probes.PeakQueries, "queries");
            Check.True(seen.Contains(TraversalKind.Vault) && seen.Contains(TraversalKind.Slide) && seen.Contains(TraversalKind.Mantle),
                "course exercises vault, slide and mantle (seen: " + string.Join(",", seen) + ")");
            Check.True(h.Motor.Probes.PeakQueries <= _t.MaxQueriesPerTick, $"probe budget exceeded: {h.Motor.Probes.PeakQueries} > {_t.MaxQueriesPerTick}");

            int traversalCheckpoints = 0;
            float worst = 0f;
            for (int start = 0; start < script.Count; start += 5)
            {
                if (history[start].Traversal != TraversalKind.None) traversalCheckpoints++;
                h.Motor.RestoreState(history[start]);
                int firstDivergence = -1;
                for (int i = start; i < script.Count; i++)
                {
                    if (firstDivergence < 0 && h.Motor.CaptureState().Position.DistanceTo(history[i].Position) > 1e-4f)
                    {
                        firstDivergence = i;
                        MotorState r0 = history[i - 1], r1 = history[i], p1 = h.Motor.CaptureState();
                        GD.Print($"[diag] tick {i - 1}->{i} cmd move={script[i - 1].Move} buttons={script[i - 1].Buttons}");
                        GD.Print($"[diag] ref  before: pos={r0.Position} vel={r0.Velocity} g={r0.IsGrounded} cr={r0.IsCrouched} trav={r0.Traversal} slideCd={r0.SlideCooldown} crouchBuf={r0.CrouchBufferRemaining}");
                        GD.Print($"[diag] ref  after : pos={r1.Position} vel={r1.Velocity} g={r1.IsGrounded} cr={r1.IsCrouched} trav={r1.Traversal}");
                        GD.Print($"[diag] replay after: pos={p1.Position} vel={p1.Velocity} g={p1.IsGrounded} cr={p1.IsCrouched} trav={p1.Traversal}");
                    }

                    h.Step(script[i]);
                }

                MotorState replay = h.Motor.CaptureState();
                float error = reference.Position.DistanceTo(replay.Position);
                worst = Mathf.Max(worst, error);
                string detail = firstDivergence < 0 ? "" :
                    $"; first divergence before tick {firstDivergence}: ref {history[firstDivergence].Traversal}/{history[firstDivergence].Position} grounded={history[firstDivergence].IsGrounded}"
                    + $" prev ref {history[firstDivergence - 1].Traversal} t={history[firstDivergence - 1].TraversalTime}";
                Check.True(error < 1e-4f, $"replay from tick {start} ({history[start].Traversal}) diverged by {error} m{detail}");
                Check.Equal(reference.Traversal, replay.Traversal, $"replay from tick {start}: traversal state");
            }

            ctx.Metric("course_replay_worst_error", worst, "m");
            Check.True(traversalCheckpoints >= 5, $"sanity: need checkpoints inside moves, got {traversalCheckpoints}");
        });
    }
}
