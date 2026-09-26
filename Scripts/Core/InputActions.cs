using Godot;

namespace Veilrun.Core;

/// <summary>
/// Cached StringNames for every input action declared in project.godot.
/// Bindings use physical keycodes so the layout works on QWERTY and AZERTY alike.
/// </summary>
public static class InputActions
{
    public static readonly StringName MoveForward = "move_forward";
    public static readonly StringName MoveBack = "move_back";
    public static readonly StringName MoveLeft = "move_left";
    public static readonly StringName MoveRight = "move_right";

    public static readonly StringName LookUp = "look_up";
    public static readonly StringName LookDown = "look_down";
    public static readonly StringName LookLeft = "look_left";
    public static readonly StringName LookRight = "look_right";

    public static readonly StringName Jump = "jump";
    public static readonly StringName Crouch = "crouch";

    public static readonly StringName ReleaseMouse = "release_mouse";

    public static readonly StringName DevToggleHud = "dev_toggle_hud";
    public static readonly StringName DevToggleDebugDraw = "dev_toggle_debug_draw";
    public static readonly StringName DevRespawn = "dev_respawn";
    public static readonly StringName DevNextSpawn = "dev_next_spawn";
}
