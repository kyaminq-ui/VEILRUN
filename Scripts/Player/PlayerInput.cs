using Godot;
using Veilrun.Core;
using Veilrun.Core.Settings;

namespace Veilrun.Player;

/// <summary>
/// Collects local device input and turns it into one <see cref="InputCommand"/> per tick.
/// Look orientation is accumulated per event/frame so aiming stays responsive at any
/// framerate, while movement intent is sampled at the fixed simulation rate.
/// </summary>
public partial class PlayerInput : Node
{
    private const float MaxPitch = 1.5533f; // 89°

    private InputButtons _latchedPresses;
    private uint _sequence;

    [Export] public UserSettings Settings { get; set; } = null!;

    /// <summary>Radians. 0 = facing -Z.</summary>
    public float Yaw { get; private set; }

    /// <summary>Radians. Positive = looking up.</summary>
    public float Pitch { get; private set; }

    public bool IsMouseCaptured => Input.MouseMode == Input.MouseModeEnum.Captured;

    public override void _Ready()
    {
        Settings ??= new UserSettings();
        CaptureMouse();
    }

    public void SetLook(float yaw, float pitch)
    {
        Yaw = Mathf.Wrap(yaw, -Mathf.Pi, Mathf.Pi);
        Pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && IsMouseCaptured)
        {
            float sens = Mathf.DegToRad(Settings.MouseSensitivity);
            float invert = Settings.InvertY ? -1f : 1f;
            SetLook(Yaw - motion.ScreenRelative.X * sens, Pitch - motion.ScreenRelative.Y * sens * invert);
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } && !IsMouseCaptured)
        {
            CaptureMouse();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(InputActions.ReleaseMouse))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            return;
        }

        // Latch presses so a tap shorter than one tick still reaches the simulation.
        if (@event.IsActionPressed(InputActions.Jump))
        {
            _latchedPresses |= InputButtons.Jump;
        }
    }

    public override void _Process(double delta)
    {
        Vector2 stick = Input.GetVector(InputActions.LookLeft, InputActions.LookRight, InputActions.LookDown, InputActions.LookUp);
        if (stick != Vector2.Zero)
        {
            float rate = Mathf.DegToRad(Settings.GamepadLookSpeed) * (float)delta;
            float invert = Settings.InvertY ? -1f : 1f;
            SetLook(Yaw - stick.X * rate, Pitch + stick.Y * rate * invert);
        }
    }

    /// <summary>Build the command for simulation tick <paramref name="tick"/>. Clears latched presses.</summary>
    public InputCommand Sample(uint tick)
    {
        Vector2 move = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveBack, InputActions.MoveForward);

        InputButtons buttons = _latchedPresses;
        if (Input.IsActionPressed(InputActions.Jump)) buttons |= InputButtons.Jump;
        if (Input.IsActionPressed(InputActions.Crouch)) buttons |= InputButtons.Crouch;
        _latchedPresses = InputButtons.None;

        return new InputCommand(++_sequence, tick, move, Yaw, Pitch, buttons);
    }

    private static void CaptureMouse()
    {
        if (DisplayServer.GetName() != "headless")
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }
}
