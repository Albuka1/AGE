using System.Diagnostics.CodeAnalysis;
using Age.Content.Sheets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The texture of a sprite and the part of it that one frame covers.
/// </summary>
/// <param name="Texture">The texture to sample, which is the placeholder of the texture service when the image is not there.</param>
/// <param name="Source">The part of the texture that is mapped onto a quad, in normalized coordinates.</param>
/// <param name="Cell">The size of one frame, in pixels, which is the size a sprite takes when it declares none.</param>
public readonly record struct SpriteRegion(TextureHandle Texture, Rect Source, Vector2 Cell)
{
    /// <summary>Returns the region that covers a whole texture, which is what a sprite without a sheet draws.</summary>
    /// <param name="texture">The texture to draw.</param>
    /// <param name="size">The size of the sprite, which is the size of what it draws.</param>
    /// <returns>The region of the whole texture.</returns>
    public static SpriteRegion Whole(TextureHandle texture, Vector2 size) =>
        new(texture, new Rect(Vector2.Zero, new Vector2(1f, 1f)), size);
}

/// <summary>
/// Reads the documents of sprite sheets, and answers with the texture of one and the region that a state and a frame cover.
/// </summary>
/// <remarks>
/// <para>
/// A sheet is the one place where the frames of a character live, so nothing else in a game writes a normalized coordinate:
/// the grid is declared in the document and the region is arithmetic over it.
/// </para>
/// <para>
/// A document that is not there, a state a sheet does not declare and a frame outside its state are answered with the
/// placeholder of the texture service and one line in the log rather than an exception in the middle of a frame. The image
/// of a sheet goes through <see cref="ITextureService.Resolve"/>, so an image that a build does not ship is the same
/// placeholder, reported the same way.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ISpriteSheetService sheets = provider.GetRequiredService&lt;ISpriteSheetService&gt;();
///
/// SpriteSheet goblin = sheets.Load("Textures/Entities/goblin.yml");
/// SpriteRegion idle = sheets.Resolve("Textures/Entities/goblin.yml", "idle", frame: 0);
/// </code>
/// </example>
public interface ISpriteSheetService
{
    /// <summary>Gets the number of sheets that are currently read.</summary>
    int Count { get; }

    /// <summary>Gets what could not be resolved, in the order it was first asked about: a document or a state of one.</summary>
    IEnumerable<string> Missing { get; }

    /// <summary>Returns the sheet a document declares, reading and caching it on the first call.</summary>
    /// <param name="relativePath">The path of the document, relative to the game root.</param>
    /// <returns>The sheet.</returns>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">There is no document at that path.</exception>
    /// <exception cref="SpriteSheetException">The document does not describe a sheet, and the error names the file and the line.</exception>
    /// <remarks>This is what a game calls at startup, where a document that is wrong is a message rather than a frame that draws the wrong cell.</remarks>
    SpriteSheet Load(string relativePath);

    /// <summary>Returns the texture of a sheet and the region that a state and a frame cover.</summary>
    /// <param name="relativePath">The path of the document of the sheet, relative to the game root.</param>
    /// <param name="state">The name of the state to draw.</param>
    /// <param name="frame">The frame inside the state, counting from zero.</param>
    /// <returns>The region to draw, or the placeholder over the whole image when the sheet or the state cannot be read.</returns>
    SpriteRegion Resolve(string relativePath, string state, int frame);

    /// <summary>Returns a state of a sheet when the document and the state can both be read.</summary>
    /// <param name="relativePath">The path of the document of the sheet, relative to the game root.</param>
    /// <param name="state">The name of the state.</param>
    /// <param name="declared">Receives the state when the sheet declares it.</param>
    /// <returns><see langword="true"/> when the sheet and its state were read.</returns>
    bool TryState(string relativePath, string state, [NotNullWhen(true)] out SpriteSheetState? declared);
}
