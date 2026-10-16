using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One window of a game: a description of what it holds, built once when the window is opened, and an update that runs
/// every frame while it is open.
/// </summary>
/// <remarks>
/// <para>
/// A window describes its elements rather than the entities it holds: what a game writes is a class that names what a
/// window of it looks like, and the elements themselves are entities with the components of the interface, which the
/// builder creates. A window that is closed and opened again is built again, so it holds no state of an element between
/// two frames: what it holds is what a game keeps on the class itself.
/// </para>
/// <para>
/// The window is registered like any other service of the engine: a class that implements this interface is found by the
/// scan of an assembly, and <see cref="UiWindowService"/> holds them all by identifier.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [System]
/// public sealed class InventoryWindow(World world) : IUiWindow
/// {
///     public string Id => "inventory";
///
///     public void Build(UiBuilder ui)
///     {
///         ui.Root.Center().Size(600f, 400f).Color(new Color(20, 20, 24));
///         ui.Label("title").Text("Inventory").At(16f, 16f).Size(200f, 24f);
///         ui.Button("close").Text("Close").At(480f, 340f).Size(100f, 32f).OnClick(() =&gt; { });
///     }
/// }
/// </code>
/// </example>
[SystemInterface]
public interface IUiWindow
{
    /// <summary>Gets the identifier of the window, which is what a game opens it by.</summary>
    string Id { get; }

    /// <summary>Describes what the window holds, which is called once each time the window is opened.</summary>
    /// <param name="ui">The builder that creates the elements of the window.</param>
    void Build(UiBuilder ui);

    /// <summary>Reads the elements the window built and updates what they show, which is called every frame.</summary>
    /// <param name="context">The window and the world it stands in, which is what an element is looked up by.</param>
    void Update(UiWindowContext context)
    {
    }
}
