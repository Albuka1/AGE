using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Bakes fonts into glyph atlases and draws text with them. A font is cached by its path and its pixel height, so
/// loading the same file at the same size twice returns the same atlas.
/// </summary>
/// <remarks>
/// The service owns every atlas it created, and it is not thread-safe: call it from the thread that owns the renderer's
/// context, which is the thread that runs the game loop. Drawing happens between <see cref="IRenderer.BeginFrame"/> and
/// <see cref="IRenderer.EndFrame"/>, so call <see cref="Draw"/> from the render callback of the loop or from a render
/// pass, next to the draws of a world. A font covers the printable ASCII range; a character outside it is drawn as a
/// space, so a line keeps its layout instead of collapsing.
/// </remarks>
/// <example>
/// <code>
/// IFontService fonts = provider.GetRequiredService&lt;IFontService&gt;();
/// FontHandle font = fonts.Load("Fonts/Cousine-Regular.ttf", 24f);
///
/// Vector2 size = fonts.Measure(font, "Hello AGE");
/// fonts.Draw(font, "Hello AGE", new Vector2(32f, 32f), Color.White);
/// </code>
/// </example>
public interface IFontService
{
    /// <summary>Gets the number of fonts that are currently loaded.</summary>
    int Count { get; }

    /// <summary>Returns the font at the given path, baking it on the first call for that height and that range of characters.</summary>
    /// <param name="relativePath">The path of the font file, relative to the game root.</param>
    /// <param name="pixelHeight">The height of a line, in pixels.</param>
    /// <param name="first">The first character of the range to bake, which is the space when a caller does not say.</param>
    /// <param name="last">The last character of the range to bake, which is the tilde when a caller does not say: the printable ASCII range.</param>
    /// <returns>The handle of the baked font.</returns>
    /// <remarks>
    /// <para>
    /// The bake covers one range of characters, and a character outside it is drawn as a space, so a line keeps its layout.
    /// The range is a pair of characters rather than a list of them, because an atlas finds the glyph of a character by
    /// arithmetic: a game that writes a language with a script of its own asks for the range that holds that script, such as
    /// the space to the end of the Cyrillic block for the Latin and Cyrillic letters of the font this repository ships.
    /// </para>
    /// <para>
    /// A wide range costs little: a character that the font has no glyph for takes no room in the atlas, so a range that spans
    /// the letters of two scripts holds the glyphs of both and nothing else.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace, or the file does not hold a font that can be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The height is zero, negative or not a finite number, the last character is before the first one, or a glyph of the font does not fit in an atlas at that size.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window.</exception>
    FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~');

    /// <summary>Determines whether the handle still refers to a font that this service loaded.</summary>
    /// <param name="font">The handle to check.</param>
    /// <returns><see langword="true"/> while the font is loaded. A handle from before an unload returns <see langword="false"/>.</returns>
    bool IsAlive(FontHandle font);

    /// <summary>Deletes the atlas behind the handle and forgets its path and height, so loading it again bakes it anew.</summary>
    /// <param name="font">The handle of the font to delete.</param>
    /// <returns><see langword="true"/> when a loaded font was deleted, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    /// <remarks>
    /// A renderer that refuses to release the atlas of the font stops the call with its own exception, and the font then
    /// stays loaded: its slot was not forgotten, so <see cref="IsAlive"/> still reports it and another call can retry the
    /// unload. Every other font is unaffected, because only this one is released here.
    /// </remarks>
    bool Unload(FontHandle font);

    /// <summary>Deletes every font that this service loaded.</summary>
    /// <remarks>
    /// An atlas whose release the renderer refused stays loaded, so another call retries it, and every other atlas is
    /// still released. The first refusal is reported, after the rest were unloaded.
    /// </remarks>
    void UnloadAll();

    /// <summary>Measures one line of text.</summary>
    /// <param name="font">The font to measure with.</param>
    /// <param name="text">The text of the line. A character outside the range of the font counts as a space.</param>
    /// <returns>The width that the text advances and the height of a line, in pixels.</returns>
    /// <exception cref="InvalidOperationException">The handle is not a live font of this service.</exception>
    /// <remarks>
    /// The width is the sum of the advances of the characters, so a caller that lays out text measures the words and the
    /// lines it builds from them. A line feed is a character like any other here: a caller that breaks a text into lines
    /// does so itself, because where a line ends is a decision about a box rather than about a string.
    /// </remarks>
    Vector2 Measure(FontHandle font, ReadOnlySpan<char> text);

    /// <summary>Returns the metrics that place a line of the font: its ascent and the distance between two baselines.</summary>
    /// <param name="font">The font to read the metrics of.</param>
    /// <returns>The metrics of the font, in pixels.</returns>
    /// <exception cref="InvalidOperationException">The handle is not a live font of this service.</exception>
    /// <remarks>
    /// <see cref="Measure"/> answers the height of a line as well, so a caller that only draws one line needs this call
    /// rarely; one that stacks lines, aligns them in a box or centers a glyph needs the baseline that a line starts at.
    /// </remarks>
    FontMetrics Metrics(FontHandle font);

    /// <summary>Draws one line of text with the top-left corner of the line at the given position.</summary>
    /// <param name="font">The font to draw with.</param>
    /// <param name="text">The text of the line. A character outside the range of the font is drawn as a space.</param>
    /// <param name="position">The top-left corner of the line, in world coordinates.</param>
    /// <param name="color">The tint of the glyphs, which are drawn as white coverage.</param>
    /// <remarks>A line feed and every other character that the font does not cover advance the pen like a space.</remarks>
    /// <exception cref="InvalidOperationException">The handle is not a live font of this service.</exception>
    void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color);
}
