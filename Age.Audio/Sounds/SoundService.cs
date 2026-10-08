using System.Runtime.ExceptionServices;
using Age.Assets;
using Age.Core;

namespace Age.Audio;

/// <summary>
/// The default <see cref="ISoundService"/>. It decodes through <see cref="ISoundLoader"/>, uploads through
/// <see cref="IAudioService"/>, and keeps the slot of every sound in a <see cref="ResourcePool{TKey, T}"/> keyed by path.
/// </summary>
/// <remarks>
/// A handle carries the slot it was issued from, so a handle from before an unload stops resolving instead of pointing
/// at the sound that replaced it. The service owns the device sounds: disposing it deletes them all. It is not
/// thread-safe, so call it from the thread that owns the audio device. A sound whose deletion the device refused stays
/// loaded, so a later call can retry it.
/// </remarks>
public sealed class SoundService : ISoundService, IDisposable
{
    private readonly ISoundLoader _sounds;
    private readonly IAudioService _audio;
    private readonly ResourcePool<string, int> _loaded = new();

    /// <summary>Initializes the service with the decoder and the audio device it works through.</summary>
    /// <param name="sounds">The loader that decodes the sound files.</param>
    /// <param name="audio">The device that holds the sounds.</param>
    /// <exception cref="ArgumentNullException">The loader or the device is null.</exception>
    public SoundService(ISoundLoader sounds, IAudioService audio)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        ArgumentNullException.ThrowIfNull(audio);
        _sounds = sounds;
        _audio = audio;
    }

    /// <inheritdoc />
    public int Count => _loaded.Count;

    /// <inheritdoc />
    public SoundHandle Load(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_loaded.TryGetHandle(relativePath, out ResourceHandle slot) && _loaded.TryGet(slot, out int cached))
        {
            return new SoundHandle(slot, cached);
        }

        SoundData sound = _sounds.Load(relativePath);
        int uploaded = _audio.CreateSound(sound);

        try
        {
            slot = _loaded.Add(uploaded, relativePath);
        }
        catch (Exception)
        {
            // The sound is registered nowhere, so delete it here instead of leaking the device sound.
            _audio.DeleteSound(uploaded);
            throw;
        }

        return new SoundHandle(slot, uploaded);
    }

    /// <inheritdoc />
    public bool IsAlive(SoundHandle sound) => sound.Resource.IsValid && _loaded.TryGet(sound.Resource, out _);

    /// <inheritdoc />
    public void Play(SoundHandle sound, float volume = 1f, bool loop = false)
    {
        if (!_loaded.TryGet(sound.Resource, out int id))
        {
            return;
        }

        _audio.PlaySound(id, volume, loop);
    }

    /// <inheritdoc />
    public bool Unload(SoundHandle sound)
    {
        if (!_loaded.TryGet(sound.Resource, out int id))
        {
            return false;
        }

        _audio.DeleteSound(id);
        _loaded.Release(sound.Resource);
        return true;
    }

    /// <inheritdoc />
    public void UnloadAll()
    {
        ExceptionDispatchInfo? failure = null;

        // Delete one sound at a time and forget a slot only once its deletion succeeded, so a device that refuses one
        // sound leaves it loaded for a later attempt and every other sound still unloads in this call.
        foreach (ResourceHandle slot in _loaded.GetHandles())
        {
            if (!_loaded.TryGet(slot, out int id))
            {
                continue;
            }

            try
            {
                _audio.DeleteSound(id);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
                continue;
            }

            _loaded.Release(slot);
        }

        failure?.Throw();
    }

    /// <summary>Deletes every sound. The service cannot be used afterwards.</summary>
    public void Dispose() => UnloadAll();
}
