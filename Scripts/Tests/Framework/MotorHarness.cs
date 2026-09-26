using Godot;
using Veilrun.Player;

namespace Veilrun.Tests.Framework;

/// <summary>
/// Drives a bare CharacterBody3D + PlayerMotor with scripted commands, without input
/// devices, cameras or the Runner node. Uses the exact same motor code as gameplay.
/// Call Step() only from inside TestContext.InPhysicsFrame.
/// </summary>
public sealed class MotorHarness
{
    private uint _tick;
    private uint _sequence;

    private MotorHarness(CharacterBody3D body, PlayerMotor motor, float dt)
    {
        Body = body;
        Motor = motor;
        Dt = dt;
    }

    public CharacterBody3D Body { get; }

    public PlayerMotor Motor { get; }

    public float Dt { get; }

    public ref readonly MotorState State => ref Motor.State;

    /// <summary>Spawn a runner body with its feet at <paramref name="feet"/>.</summary>
    public static MotorHarness Create(TestContext ctx, MovementTuning tuning, Vector3 feet)
    {
        var body = new CharacterBody3D { Name = "Body" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = tuning.CapsuleRadius, Height = tuning.CapsuleHeight },
            Position = new Vector3(0, tuning.CapsuleHeight * 0.5f, 0),
        });
        ctx.Sandbox.AddChild(body);
        body.GlobalPosition = feet;
        return new MotorHarness(body, new PlayerMotor(body, tuning), ctx.PhysicsDelta);
    }

    public InputCommand Command(Vector2 move, float yaw = 0f, InputButtons buttons = InputButtons.None) =>
        new(++_sequence, ++_tick, move, yaw, 0f, buttons);

    public MotorEvents Step(Vector2 move, float yaw = 0f, InputButtons buttons = InputButtons.None) =>
        Motor.Simulate(Command(move, yaw, buttons), Dt);

    public MotorEvents Step(in InputCommand cmd) => Motor.Simulate(cmd, Dt);

    /// <summary>Remove the body from the physics space immediately (so later runners can't hit it), then free it.</summary>
    public void Dispose()
    {
        Body.GetParent()?.RemoveChild(Body);
        Body.QueueFree();
    }

    /// <summary>Idle until grounded (or give up after maxTicks). Returns true when grounded.</summary>
    public bool Settle(int maxTicks = 240)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            Step(Vector2.Zero);
            if (State.IsGrounded && i > 2)
            {
                return true;
            }
        }

        return State.IsGrounded;
    }
}

/// <summary>Static collision fixtures for tests (StaticBody3D + BoxShape3D, no visuals).</summary>
public static class TestWorld
{
    public static StaticBody3D Box(TestContext ctx, Vector3 center, Vector3 size, Vector3? rotationDegrees = null)
    {
        var body = new StaticBody3D { Name = "Box" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        ctx.Sandbox.AddChild(body);
        body.GlobalPosition = center;
        if (rotationDegrees is { } r)
        {
            body.RotationDegrees = r;
        }

        return body;
    }

    /// <summary>Large flat floor whose top surface is at <paramref name="topY"/>.</summary>
    public static StaticBody3D Floor(TestContext ctx, float topY = 0f, float size = 400f) =>
        Box(ctx, new Vector3(0, topY - 0.5f, 0), new Vector3(size, 1f, size));
}
