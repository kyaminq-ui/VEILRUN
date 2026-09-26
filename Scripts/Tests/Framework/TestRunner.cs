using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace Veilrun.Tests.Framework;

/// <summary>
/// Headless in-engine test runner. Discovers [Test] methods by reflection, runs each in a
/// fresh sandbox with real Jolt physics, prints results + measured metrics, writes
/// user://test_results.json and quits with exit code 0 (all pass) or 1.
///
/// Usage: tools/run_tests.ps1   (or)   Godot --headless --path . res://Scenes/Tests/TestRunner.tscn -- --filter=Motor
/// </summary>
public partial class TestRunner : Node
{
    private readonly List<TestMetric> _metrics = new();

    private sealed record Result(string Name, bool Passed, string? Error, double Ms);

    public override void _Ready() => CallDeferred(MethodName.RunAll);

    private async void RunAll()
    {
        string? filter = OS.GetCmdlineUserArgs()
            .FirstOrDefault(a => a.StartsWith("--filter=", StringComparison.Ordinal))?["--filter=".Length..];

        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
                .Select(m => (Type: t, Method: m, Name: $"{t.Name}.{m.Name}")))
            .Where(x => filter is null || x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        GD.Print($"[Tests] Running {tests.Count} test(s){(filter is null ? "" : $" matching '{filter}'")} at {Engine.PhysicsTicksPerSecond} Hz");
        var results = new List<Result>();

        foreach (var (type, method, name) in tests)
        {
            var sandbox = new Node3D { Name = "Sandbox" };
            AddChild(sandbox);
            // Let static fixtures register with the physics server before the test queries it.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var ctx = new TestContext(name, sandbox, _metrics);
            var sw = Stopwatch.StartNew();
            string? error = null;
            try
            {
                object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
                object?[] args = method.GetParameters().Length == 1 ? new object?[] { ctx } : Array.Empty<object?>();
                if (method.Invoke(instance, args) is Task task)
                {
                    await task;
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                error = Describe(tie.InnerException);
            }
            catch (Exception ex)
            {
                error = Describe(ex);
            }

            sw.Stop();
            results.Add(new Result(name, error is null, error, sw.Elapsed.TotalMilliseconds));
            GD.Print(error is null ? $"  PASS  {name} ({sw.Elapsed.TotalMilliseconds:0} ms)" : $"  FAIL  {name}\n        {error}");

            sandbox.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        int failed = results.Count(r => !r.Passed);
        if (_metrics.Count > 0)
        {
            GD.Print("[Tests] Measured metrics:");
            foreach (TestMetric m in _metrics)
            {
                GD.Print($"  METRIC {m.Name,-34} {m.Value,10:0.000} {m.Unit}");
            }
        }

        GD.Print($"[Tests] {results.Count - failed} passed, {failed} failed");
        WriteReport(results);
        GetTree().Quit(failed == 0 && results.Count > 0 ? 0 : 1);
    }

    private void WriteReport(List<Result> results)
    {
        var sb = new StringBuilder();
        sb.Append("{\"engine\":\"").Append(Engine.GetVersionInfo()["string"].AsString())
          .Append("\",\"physics_hz\":").Append(Engine.PhysicsTicksPerSecond).Append(",\"results\":[");
        sb.AppendJoin(',', results.Select(r =>
            $"{{\"name\":\"{r.Name}\",\"passed\":{(r.Passed ? "true" : "false")},\"ms\":{r.Ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\"error\":{(r.Error is null ? "null" : Json.Stringify(r.Error))}}}"));
        sb.Append("],\"metrics\":[");
        sb.AppendJoin(',', _metrics.Select(m =>
            $"{{\"test\":\"{m.Test}\",\"name\":\"{m.Name}\",\"value\":{m.Value.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)},\"unit\":\"{m.Unit}\"}}"));
        sb.Append("]}");

        const string path = "user://test_results.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(sb.ToString());
        GD.Print($"[Tests] Report: {ProjectSettings.GlobalizePath(path)}");
    }

    private static string Describe(Exception ex) =>
        ex is TestFailure ? ex.Message : $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
}
