namespace Age.Core;

/// <summary>
/// Registers the components that live in the core assembly.
/// </summary>
internal sealed class CoreComponentRegistrations : IComponentRegistrations
{
    /// <inheritdoc />
    public void Register(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register("Transform", AgeJsonContext.Default.TransformComponent);
    }
}
