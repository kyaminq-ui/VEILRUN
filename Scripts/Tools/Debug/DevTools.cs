using System.Collections.Generic;
using Godot;
using Veilrun.Core;

namespace Veilrun.Tools.Debug;

/// <summary>
/// Autoload (/root/DevTools) owning the Dev HUD and debug draw. Systems register as
/// <see cref="IDebugInfoProvider"/>. Lookup goes through <see cref="Find"/> so callers
/// handle its absence (e.g. release builds, isolated tests) instead of relying on a static.
/// </summary>
public partial class DevTools : Node
{
    public const string AutoloadPath = "/root/DevTools";

    private readonly List<IDebugInfoProvider> _providers = new();
    private DevHud _hud = null!;

    public DebugDraw Draw { get; private set; } = null!;

    public static DevTools? Find(Node context) => context.GetNodeOrNull<DevTools>(AutoloadPath);

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _hud = new DevHud { Name = "DevHud", Visible = OS.IsDebugBuild() };
        _hud.SetProviders(_providers);
        AddChild(_hud);

        Draw = new DebugDraw { Name = "DebugDraw", Enabled = false };
        AddChild(Draw);
    }

    public void Register(IDebugInfoProvider provider)
    {
        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
        }
    }

    public void Unregister(IDebugInfoProvider provider) => _providers.Remove(provider);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.DevToggleHud))
        {
            _hud.Visible = !_hud.Visible;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.DevToggleDebugDraw))
        {
            Draw.Enabled = !Draw.Enabled;
            if (!Draw.Enabled)
            {
                Draw.Clear();
            }

            Log.Info("Dev", $"Debug draw {(Draw.Enabled ? "ON" : "OFF")}");
            GetViewport().SetInputAsHandled();
        }
    }
}
