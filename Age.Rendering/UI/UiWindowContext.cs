using Age.Core;

namespace Age.Rendering;

/// <summary>
/// What one frame of an open window is handed: the world the window stands in, the service that opened it, and the
/// identifier of the window itself.
/// </summary>
/// <remarks>
/// The context is created per frame rather than kept, because a world outlives neither the run nor a scene that replaced
/// it: a window reads the world it is handed rather than the one it was opened in, so a run that loads a scene and keeps
/// its windows reaches the elements of the scene that is there now.
/// </remarks>
public sealed class UiWindowContext
{
    private readonly UiWindowService _service;

    internal UiWindowContext(World world, UiWindowService service, string windowId, Entity canvas)
    {
        World = world;
        _service = service;
        WindowId = windowId;
        Canvas = canvas;
    }

    /// <summary>Gets the world the window stands in.</summary>
    public World World { get; }

    /// <summary>Gets the identifier of the window, which is what <see cref="Find"/> reads by.</summary>
    public string WindowId { get; }

    /// <summary>Gets the entity of the canvas that the window stands in, which is what holds the tree.</summary>
    public Entity Canvas { get; }

    /// <summary>Returns the entity of an element the window built, or null when it holds no element of that name.</summary>
    /// <param name="elementId">The name the element was built under.</param>
    /// <returns>The entity of the element, or null.</returns>
    public Entity? Find(string elementId) => _service.Find(World, WindowId, elementId);
}
