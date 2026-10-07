using Microsoft.Extensions.DependencyInjection;

namespace Age.Assets;

/// <summary>
/// Registers the asset services of the engine.
/// </summary>
public static class AssetServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAssetLoader"/> as a singleton backed by <see cref="NullAssetLoader"/>,
    /// <see cref="IImageLoader"/> as a singleton backed by <see cref="StbImageLoader"/> and
    /// <see cref="ISoundLoader"/> as a singleton backed by <see cref="SoundLoader"/>, which reads WAVE, MPEG audio and
    /// Ogg Vorbis.
    /// </summary>
    public static IServiceCollection AddAgeAssets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAssetLoader, NullAssetLoader>();
        services.AddSingleton<IImageLoader, StbImageLoader>();
        services.AddSingleton<ISoundLoader, SoundLoader>();
        return services;
    }
}
