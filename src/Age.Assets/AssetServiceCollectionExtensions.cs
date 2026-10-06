using Microsoft.Extensions.DependencyInjection;

namespace Age.Assets;

/// <summary>
/// Registers the asset services of the engine.
/// </summary>
public static class AssetServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAssetLoader"/> as a singleton backed by <see cref="NullAssetLoader"/> and
    /// <see cref="IImageLoader"/> as a singleton backed by <see cref="StbImageLoader"/>.
    /// </summary>
    public static IServiceCollection AddAgeAssets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAssetLoader, NullAssetLoader>();
        services.AddSingleton<IImageLoader, StbImageLoader>();
        return services;
    }
}
