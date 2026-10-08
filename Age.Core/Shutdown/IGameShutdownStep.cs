namespace Age.Core;

/// <summary>
/// One step of the shutdown of a game, which releases what the assembly that registered it holds.
/// </summary>
/// <remarks>
/// A game releases device objects in an order: the atlases and the textures of a frame before the renderer that uploaded
/// them, and the renderer before the window whose context it draws in. Every assembly knows that order for what it owns,
/// so a step is registered next to the thing it releases and the game calls one method instead of repeating the order.
/// A game registers steps of its own for what it holds, which run wherever its order puts them.
/// </remarks>
/// <example>
/// <code>
/// internal sealed class PlayerShutdownStep : IGameShutdownStep
/// {
///     public int Order => 600;
///
///     public void Shutdown() => _session.Save();
/// }
/// </code>
/// </example>
public interface IGameShutdownStep
{
    /// <summary>Gets the position of the step in the order of the shutdown. The steps run from the lowest order to the highest.</summary>
    int Order { get; }

    /// <summary>Releases what this step owns.</summary>
    /// <remarks>The step has to be safe to run twice, because the container disposes the same services afterwards.</remarks>
    void Shutdown();
}
