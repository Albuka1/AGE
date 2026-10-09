using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Measures a text with one baked font of an <see cref="IFontService"/>.
/// </summary>
/// <remarks>
/// This is the measurer of a text that names one font, which is what a line of a world is: the font service holds the
/// metrics of the atlas and the advance of every character of it, so measuring is a walk over the characters and no device
/// object is touched. A handle whose font was unloaded stops resolving, which is the failure the service reports when the
/// text is measured rather than when it is drawn.
/// </remarks>
public sealed class FontTextMeasurer : ITextMeasurer
{
    private readonly IFontService _fonts;
    private readonly FontHandle _font;

    /// <summary>Initializes the measurer with the service that baked the font and the handle of the font.</summary>
    /// <param name="fonts">The service that holds the baked font.</param>
    /// <param name="font">The handle of the font to measure with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fonts"/> is null.</exception>
    public FontTextMeasurer(IFontService fonts, FontHandle font)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
        _font = font;
    }

    /// <inheritdoc />
    /// <remarks>The metrics are asked for on every read, so a font that was unloaded says so instead of reporting the numbers of an atlas that is gone.</remarks>
    public FontMetrics Metrics => _fonts.Metrics(_font);

    /// <inheritdoc />
    public Vector2 Measure(ReadOnlySpan<char> text) => _fonts.Measure(_font, text);

    /// <inheritdoc />
    /// <remarks>One font measures and draws the whole text, and a character outside its range is drawn as its space.</remarks>
    public FontHandle? Font(char value) => _font;
}
