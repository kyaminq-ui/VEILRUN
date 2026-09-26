using System.Collections.Generic;
using System.Text;
using Godot;

namespace Veilrun.Tools.Debug;

/// <summary>
/// Text overlay listing engine stats plus one section per registered provider.
/// Refreshes at a fixed low rate to keep string building off the per-frame budget.
/// </summary>
public partial class DevHud : CanvasLayer
{
    private const double RefreshInterval = 0.1;

    private readonly StringBuilder _sb = new(2048);
    private IReadOnlyList<IDebugInfoProvider> _providers = System.Array.Empty<IDebugInfoProvider>();
    private Label _label = null!;
    private double _sinceRefresh;

    public override void _Ready()
    {
        Layer = 100;

        var panel = new PanelContainer
        {
            Position = new Vector2(8, 8),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.03f, 0.05f, 0.72f),
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        });

        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _label.AddThemeFontOverride("font", new SystemFont { FontNames = new[] { "Consolas", "Cascadia Mono", "Courier New", "monospace" } });
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 1f));

        panel.AddChild(_label);
        AddChild(panel);
    }

    public void SetProviders(IReadOnlyList<IDebugInfoProvider> providers) => _providers = providers;

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (!Visible || _sinceRefresh < RefreshInterval)
        {
            return;
        }

        _sinceRefresh = 0;
        _sb.Clear();
        _sb.AppendLine("VEILRUN DEV   F1 hud · F2 debug draw · F4 respawn · Esc mouse");
        double fps = Engine.GetFramesPerSecond();
        _sb.Append("FPS ").Append(fps.ToString("0"))
           .Append("   frame ").Append((Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0).ToString("0.00")).Append(" ms")
           .Append("   physics ").Append((Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0).ToString("0.00")).Append(" ms @ ")
           .Append(Engine.PhysicsTicksPerSecond).AppendLine(" Hz");
        _sb.AppendLine("NET  offline   ping —   loss —   (networking lands in M3)");

        foreach (IDebugInfoProvider provider in _providers)
        {
            _sb.Append("── ").Append(provider.DebugTitle).AppendLine(" ──");
            provider.AppendDebugInfo(_sb);
        }

        // AppendLine emits "\r\n" on Windows, which Label renders as two line breaks.
        _label.Text = _sb.Replace("\r", string.Empty).ToString();
    }
}
