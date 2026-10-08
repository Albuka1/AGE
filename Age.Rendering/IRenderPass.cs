using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws one pass of a frame. <see cref="RenderPipeline"/> runs the passes in the order in which they were added.
/// </summary>
/// <remarks>
/// A pass draws with the renderer it was given and does not own the frame: <see cref="RenderPipeline.Render"/> calls
/// every pass in order, and each of them begins and ends its own frame. The passes of the engine are
/// <see cref="RenderSystem"/>, which draws the world through the camera it receives, and <see cref="UIRenderSystem"/>,
/// which draws the screen-space UI on top and ignores the camera.
/// </remarks>
public interface IRenderPass
{
    /// <summary>Draws the pass.</summary>
    /// <param name="world">The world to draw.</param>
    /// <param name="camera">The camera of the frame. A pass that draws in screen space ignores it.</param>
    void Render(World world, in Camera2D camera);
}
