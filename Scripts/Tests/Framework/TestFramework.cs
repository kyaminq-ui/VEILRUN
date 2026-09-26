using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Veilrun.Tests.Framework;

/// <summary>Marks a test method. Signature: void/Task M() or void/Task M(TestContext ctx).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
}

public sealed class TestFailure : Exception
{
    public TestFailure(string message) : base(message)
    {
    }
}

public readonly record struct TestMetric(string Test, string Name, double Value, string Unit);

/// <summary>Per-test environment: an isolated 3D sandbox inside the live SceneTree.</summary>
public sealed class TestContext
{
    private readonly List<TestMetric> _metrics;

    internal TestContext(string testName, Node3D sandbox, List<TestMetric> metrics)
    {
        TestName = testName;
        Sandbox = sandbox;
        _metrics = metrics;
    }

    public string TestName { get; }

    /// <summary>Freed after the test. Put all fixture nodes under it.</summary>
    public Node3D Sandbox { get; }

    public float PhysicsDelta => (float)Sandbox.GetPhysicsProcessDeltaTime();

    public SignalAwaiter PhysicsFrame() => Sandbox.ToSignal(Sandbox.GetTree(), SceneTree.SignalName.PhysicsFrame);

    /// <summary>
    /// Waits for the next physics frame, then runs <paramref name="body"/> synchronously inside it.
    /// Motor simulation must run inside a physics frame (MoveAndSlide integrates over the physics step),
    /// and running many ticks in one frame is exactly what reconciliation replay does.
    /// </summary>
    public async Task InPhysicsFrame(Action body)
    {
        await PhysicsFrame();
        Check.True(Engine.IsInPhysicsFrame(), "harness expected to be inside a physics frame");
        body();
    }

    public void Metric(string name, double value, string unit) => _metrics.Add(new TestMetric(TestName, name, value, unit));
}

public static class Check
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new TestFailure(message);
        }
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestFailure($"{message}: expected {expected}, got {actual}");
        }
    }

    public static void Near(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new TestFailure($"{message}: expected {expected:0.####} ± {tolerance:0.####}, got {actual:0.####}");
        }
    }

    public static void InRange(double value, double min, double max, string message)
    {
        if (value < min || value > max)
        {
            throw new TestFailure($"{message}: expected [{min:0.####}, {max:0.####}], got {value:0.####}");
        }
    }
}
