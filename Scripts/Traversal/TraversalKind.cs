namespace Veilrun.Traversal;

/// <summary>Exclusive parkour move the runner is currently performing (None = locomotion).</summary>
public enum TraversalKind : byte
{
    None,
    Slide,
    Vault,
    Mantle,
    LedgeHang,
    LedgeClimb,
    WallRun,
    WallClimb,
    Roll,
}
