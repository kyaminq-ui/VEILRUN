using Godot;

namespace Veilrun.Core;

public static class MathUtil
{
    /// <summary>Framerate-independent exponential smoothing toward <paramref name="target"/>.</summary>
    public static float ExpDecay(float current, float target, float rate, float dt) =>
        target + (current - target) * Mathf.Exp(-rate * dt);
}
