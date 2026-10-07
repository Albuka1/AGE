using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Registers the components that live in the rendering assembly.
/// </summary>
/// <remarks>
/// A sprite keeps its texture handle, whose device identifier does not survive a reload: load the texture again and set
/// it on the component after the scene was loaded.
/// </remarks>
internal sealed class RenderingComponentRegistrations : IComponentRegistrations
{
    /// <inheritdoc />
    public void Register(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register("Sprite", AgeRenderingJsonContext.Default.SpriteComponent);
    }
}
