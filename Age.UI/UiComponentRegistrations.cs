using Age.Core;

namespace Age.UI;

/// <summary>
/// Registers the components that live in the UI assembly.
/// </summary>
/// <remarks>
/// A button carries the state of its interaction, which is written with the rest of the component: a scene that was
/// saved while the pointer was over a button comes back with that state.
/// </remarks>
internal sealed class UiComponentRegistrations : IComponentRegistrations
{
    /// <inheritdoc />
    public void Register(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register("RectTransform", AgeUiJsonContext.Default.RectTransformComponent);
        registry.Register("Button", AgeUiJsonContext.Default.ButtonComponent);
        registry.Register("TextLabel", AgeUiJsonContext.Default.TextLabelComponent);
    }
}
