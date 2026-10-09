using System.Diagnostics.CodeAnalysis;
using Age.Assets;
using Age.Content.Sheets;
using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="ISpriteSheetService"/>: it reads a sheet document through <see cref="IAssetLoader"/> and resolves the
/// image of a sheet through <see cref="ITextureService"/>, so a sheet is content that draws.
/// </summary>
/// <remarks>
/// A sheet is read once and kept, and a document or a state that cannot be resolved is remembered as well: the log hears
/// about it once rather than on every frame, and the frame draws the placeholder of the texture service in the meantime.
/// </remarks>
public sealed class SpriteSheetService : ISpriteSheetService
{
    private readonly IAssetLoader _assets;
    private readonly ITextureService _textures;
    private readonly ILogger<SpriteSheetService>? _logger;
    private readonly Dictionary<string, SpriteSheet> _sheets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly List<string> _missing = [];

    /// <summary>Initializes the service with the files of the game and the textures it draws.</summary>
    /// <param name="assets">The loader that reads the documents.</param>
    /// <param name="textures">The service that resolves the image a sheet names.</param>
    /// <param name="logger">The logger that reports what could not be resolved, or null to report nothing.</param>
    /// <exception cref="ArgumentNullException">The loader or the texture service is null.</exception>
    public SpriteSheetService(IAssetLoader assets, ITextureService textures, ILogger<SpriteSheetService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(textures);
        _assets = assets;
        _textures = textures;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Count => _sheets.Count;

    /// <inheritdoc />
    public IEnumerable<string> Missing => _missing;

    /// <inheritdoc />
    public SpriteSheet Load(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_sheets.TryGetValue(relativePath, out SpriteSheet? loaded))
        {
            return loaded;
        }

        if (_failed.TryGetValue(relativePath, out Exception? failure))
        {
            // The document was read once, and what was wrong with it is kept: a game that asks again hears the same mistake
            // without the log hearing it twice.
            throw failure;
        }

        try
        {
            SpriteSheet sheet = SpriteSheetReader.Read(_assets.Load<string>(relativePath), relativePath);
            _sheets[relativePath] = sheet;
            return sheet;
        }
        catch (Exception exception) when (exception is IOException or SpriteSheetException)
        {
            _failed[relativePath] = exception;
            Report(relativePath, $"The sprite sheet '{relativePath}' cannot be read: {exception.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public SpriteRegion Resolve(string relativePath, string state, int frame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(state);

        if (Sheet(relativePath) is not SpriteSheet sheet)
        {
            return Placeholder();
        }

        if (!sheet.TryGetState(state, out SpriteSheetState? declared))
        {
            Report($"{relativePath}:{state}", $"The sprite sheet '{relativePath}' declares no state '{state}', so the sprite is drawn as the placeholder: its states are {string.Join(", ", sheet.States.Keys)}.");
            return Placeholder();
        }

        if (frame < 0 || frame >= declared.Frames)
        {
            Report($"{relativePath}:{state}:{frame}", $"The state '{state}' of the sprite sheet '{relativePath}' holds {declared.Frames} frames, and frame {frame} was asked for. The first frame of the state is drawn instead.");

            // A frame that does not exist is a mistake of what drives the animation rather than of the content, so the
            // first frame of the state is drawn: a sprite that stands still beats a pink square.
            frame = 0;
        }

        return new SpriteRegion(_textures.Resolve(sheet.Image), sheet.Region(declared, frame), sheet.Cell);
    }

    /// <inheritdoc />
    public bool TryState(string relativePath, string state, [NotNullWhen(true)] out SpriteSheetState? declared)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(state);

        if (Sheet(relativePath) is SpriteSheet sheet)
        {
            if (sheet.TryGetState(state, out declared))
            {
                return true;
            }

            Report($"{relativePath}:{state}", $"The sprite sheet '{relativePath}' declares no state '{state}', so nothing is played: its states are {string.Join(", ", sheet.States.Keys)}.");
        }

        declared = null;
        return false;
    }

    /// <summary>Returns the sheet of a document, reading it once, or null when it cannot be read.</summary>
    private SpriteSheet? Sheet(string relativePath)
    {
        if (_sheets.TryGetValue(relativePath, out SpriteSheet? loaded))
        {
            return loaded;
        }

        if (_failed.ContainsKey(relativePath))
        {
            return null;
        }

        try
        {
            SpriteSheet sheet = SpriteSheetReader.Read(_assets.Load<string>(relativePath), relativePath);
            _sheets[relativePath] = sheet;
            return sheet;
        }
        catch (Exception exception) when (exception is IOException or SpriteSheetException)
        {
            _failed[relativePath] = exception;
            Report(relativePath, $"The sprite sheet '{relativePath}' cannot be read, so the sprite is drawn as the placeholder: {exception.Message}");
            return null;
        }
    }

    /// <summary>Returns the placeholder over the whole image, which is what a sheet that cannot be resolved draws.</summary>
    private SpriteRegion Placeholder() => SpriteRegion.Whole(_textures.Error, new Vector2(64f, 64f));

    /// <summary>Leaves one record and one line in the log for what could not be resolved, whatever asks for it again.</summary>
    private void Report(string what, string message)
    {
        if (_reported.Add(what))
        {
            _missing.Add(what);
            _logger?.LogError("{Reason}", message);
        }
    }
}
