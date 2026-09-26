using Godot;

namespace Veilrun.Core;

/// <summary>
/// First autoload. Performs process-wide setup that must exist before any scene runs.
/// Keep this tiny: it is a bootstrapper, not a service locator.
/// </summary>
public partial class Boot : Node
{
    public override void _EnterTree()
    {
        InputDefaults.Register();
        Log.Info("Boot", $"VEILRUN {ProjectSettings.GetSetting("application/config/version", "dev")} · Godot {Engine.GetVersionInfo()["string"]} · physics {Engine.PhysicsTicksPerSecond} Hz");
    }
}
