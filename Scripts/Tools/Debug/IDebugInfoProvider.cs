using System.Text;

namespace Veilrun.Tools.Debug;

/// <summary>Anything that wants a section in the Dev HUD implements this and registers with DevTools.</summary>
public interface IDebugInfoProvider
{
    string DebugTitle { get; }

    void AppendDebugInfo(StringBuilder sb);
}
