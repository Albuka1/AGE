using Age.Core;

namespace Age.Audio;

/// <summary>
/// Releases what the audio assembly holds: every loaded sample, before the window that the device was created for goes.
/// </summary>
internal sealed class AudioShutdownStep : IGameShutdownStep
{
    private readonly ISoundService _sounds;

    /// <summary>Initializes the step from the service that owns the loaded samples.</summary>
    /// <param name="sounds">The sounds, which own the buffers of the device.</param>
    public AudioShutdownStep(ISoundService sounds) => _sounds = sounds;

    /// <inheritdoc />
    public int Order => 400;

    /// <inheritdoc />
    public void Shutdown() => _sounds.UnloadAll();
}
