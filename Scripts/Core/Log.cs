using Godot;

namespace Veilrun.Core;

/// <summary>
/// Thin categorized logging facade. Keeps a single choke point so we can later route
/// logs to files, server analytics or anomaly reports without touching call sites.
/// </summary>
public static class Log
{
    public static void Info(string category, string message) => GD.Print($"[{category}] {message}");

    public static void Warn(string category, string message) => GD.PushWarning($"[{category}] {message}");

    public static void Error(string category, string message) => GD.PushError($"[{category}] {message}");
}
