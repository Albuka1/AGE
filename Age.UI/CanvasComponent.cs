using Age.Core;

namespace Age.UI;

/// <summary>
/// Marks a UI hierarchy root.
/// </summary>
public struct CanvasComponent : IComponent
{
    /// <summary>Gets or sets a value indicating whether this canvas is the root of its hierarchy.</summary>
    public bool IsRoot;
}
