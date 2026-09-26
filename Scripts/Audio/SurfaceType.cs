using Godot;

namespace Veilrun.Audio;

/// <summary>
/// Material of a walkable/touchable surface. Authored on any collider (StaticBody3D, CSG…)
/// as metadata <c>surface = "metal"</c> (case-insensitive enum name). Missing = Concrete.
/// Extend freely; audio banks fall back to Concrete for surfaces without their own set.
/// </summary>
public enum SurfaceType : byte
{
    Concrete,
    Metal,
    MetalGrate,
    Glass,
    Gravel,
    Wood,
    Rubber,
    Water,
}

public static class Surfaces
{
    public static readonly StringName MetaKey = "surface";

    public static SurfaceType Resolve(GodotObject? collider)
    {
        if (collider is Node node && node.HasMeta(MetaKey)
            && System.Enum.TryParse(node.GetMeta(MetaKey).AsString(), ignoreCase: true, out SurfaceType type))
        {
            return type;
        }

        return SurfaceType.Concrete;
    }
}
