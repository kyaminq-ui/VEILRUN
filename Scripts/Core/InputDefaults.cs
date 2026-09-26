using Godot;

namespace Veilrun.Core;

/// <summary>
/// Default input bindings, registered at boot. Keyboard keys use PHYSICAL keycodes, so the
/// W/A/S/D positions map to Z/Q/S/D on AZERTY with no extra configuration.
/// Actions already bound (e.g. edited in Project Settings, or future user overrides from
/// the rebinding menu) are left untouched. See docs/DECISIONS.md D-008.
/// </summary>
public static class InputDefaults
{
    public static void Register()
    {
        Bind(InputActions.MoveForward, 0.2f, Key(Godot.Key.W), Axis(JoyAxis.LeftY, -1f));
        Bind(InputActions.MoveBack, 0.2f, Key(Godot.Key.S), Axis(JoyAxis.LeftY, 1f));
        Bind(InputActions.MoveLeft, 0.2f, Key(Godot.Key.A), Axis(JoyAxis.LeftX, -1f));
        Bind(InputActions.MoveRight, 0.2f, Key(Godot.Key.D), Axis(JoyAxis.LeftX, 1f));

        Bind(InputActions.LookUp, 0.15f, Axis(JoyAxis.RightY, -1f));
        Bind(InputActions.LookDown, 0.15f, Axis(JoyAxis.RightY, 1f));
        Bind(InputActions.LookLeft, 0.15f, Axis(JoyAxis.RightX, -1f));
        Bind(InputActions.LookRight, 0.15f, Axis(JoyAxis.RightX, 1f));

        Bind(InputActions.Jump, 0.5f, Key(Godot.Key.Space), Button(JoyButton.A));
        Bind(InputActions.Crouch, 0.5f, Key(Godot.Key.Ctrl), Key(Godot.Key.C), Button(JoyButton.B));

        Bind(InputActions.ReleaseMouse, 0.5f, Key(Godot.Key.Escape));

        Bind(InputActions.DevToggleHud, 0.5f, Key(Godot.Key.F1));
        Bind(InputActions.DevToggleDebugDraw, 0.5f, Key(Godot.Key.F2));
        Bind(InputActions.DevRespawn, 0.5f, Key(Godot.Key.F4));
    }

    private static void Bind(StringName action, float deadzone, params InputEvent[] events)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action, deadzone);
        }

        if (InputMap.ActionGetEvents(action).Count > 0)
        {
            return;
        }

        foreach (InputEvent e in events)
        {
            InputMap.ActionAddEvent(action, e);
        }
    }

    private static InputEventKey Key(Key key) => new() { PhysicalKeycode = key };

    private static InputEventJoypadButton Button(JoyButton button) => new() { ButtonIndex = button, Device = -1 };

    private static InputEventJoypadMotion Axis(JoyAxis axis, float value) => new() { Axis = axis, AxisValue = value, Device = -1 };
}
