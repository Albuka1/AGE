using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// A picture inside the rectangle of the element, which is what an icon of an item and a frame of a portrait is.
/// </summary>
public sealed class ImageHandle : UiHandle<ImageHandle>
{
    internal ImageHandle(World world, Entity entity) : base(world, entity)
    {
    }

    /// <summary>Sets the path of the image, relative to the game root.</summary>
    /// <param name="path">The path of a file the build ships.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ImageHandle Source(string path)
    {
        World.GetRef<ImageComponent>(Entity).TexturePath = path;
        return this;
    }

    /// <summary>Sets the tint the image is multiplied by.</summary>
    /// <param name="color">The colour of the element, where white draws the image as it is.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ImageHandle Tint(Color color)
    {
        World.GetRef<ImageComponent>(Entity).Tint = color;
        return this;
    }
}
