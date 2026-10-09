using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// Draws the text of an entity: it resolves the string, lays it out for the box it stands in, draws the lines, and writes
/// what it resolved back into the component.
/// </summary>
/// <remarks>
/// <para>
/// Both the pass of a world and the pass of the interface draw text, and the only difference between them is where a line
/// stands: a line of a world starts at the transform of its entity, a label of the interface in its rectangle. This type
/// holds what the two have in common, so a key of the strings of a game, a stack of fonts, a box that cuts a text off and a
/// font that cannot be baked behave the same in either place.
/// </para>
/// <para>
/// A text is laid out once for as long as nothing about it changes: the key, the count, the text, the language of the game,
/// the box, the style and the fonts of an entity are remembered together with the layout, so a frame that draws the same
/// text again draws what the last one laid out rather than measuring a string it already measured. The language is part of
/// what is remembered, so switching the language of a game lays the text out again without anyone telling this type that it
/// happened.
/// </para>
/// <para>
/// A line whose characters come from more than one font of the stack is drawn in runs, one run per font, which is what lets
/// a line hold the letters of two scripts. A font that cannot be baked is reported once per path and the characters it would
/// have covered are drawn by the font below it in the stack, or by the built-in font when the stack holds nothing else, and
/// a key that no language of the game answers is drawn as the key itself, which is what the source answers for it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// TextRenderer text = provider.GetRequiredService&lt;TextRenderer&gt;();
///
/// // A line of a world at the transform of its entity, as large as it needs:
/// text.Draw(world, entity, transform.Position, component.Box, component);
///
/// // A label of the interface in the box of its rectangle:
/// text.Draw(world, entity, rect.Position, component.Box == Vector2.Zero ? rect.Size : component.Box, component);
/// </code>
/// </example>
public sealed class TextRenderer
{
    /// <summary>The number of texts whose layout is remembered before the ones that are not drawn again are forgotten.</summary>
    /// <remarks>A world with more texts than this lays the first ones out again, which costs a measurement rather than memory that grows without a bound.</remarks>
    private const int MaximumRemembered = 512;

    private readonly IRenderer _renderer;
    private readonly IFontService? _fonts;
    private readonly ITextSource _text;
    private readonly ILogger<TextRenderer>? _logger;
    private readonly FontStyle[] _defaults;
    private readonly Dictionary<Entity, LaidOut> _remembered = new();
    private readonly Dictionary<string, Cached> _lines = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    /// <summary>Initializes the renderer of text.</summary>
    /// <param name="renderer">The renderer that draws the glyphs and owns the built-in font.</param>
    /// <param name="fonts">The service that bakes the fonts of a style, or null to draw every text with the built-in font.</param>
    /// <param name="text">The source of the strings of a game, or null to answer every key with the key itself.</param>
    /// <param name="logger">The log of the game, or null to report nothing.</param>
    /// <param name="defaults">The fonts that a text which names none of its own is drawn with, or null for the built-in font.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is null.</exception>
    /// <remarks>
    /// A container passes <see cref="TextDefaults.Fonts"/> as the defaults, which is what makes a text of a game readable
    /// without the game naming a font.
    /// </remarks>
    public TextRenderer(IRenderer renderer, IFontService? fonts = null, ITextSource? text = null, ILogger<TextRenderer>? logger = null, FontStyle[]? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        _fonts = fonts;
        _text = text ?? KeyTextSource.Instance;
        _logger = logger;
        _defaults = defaults ?? [];
    }

    /// <summary>Draws the text of an entity into a box whose top-left corner is at a position.</summary>
    /// <param name="world">The world that holds the entity, which what was resolved is written back into.</param>
    /// <param name="entity">The entity that carries the text.</param>
    /// <param name="position">The top-left corner of the box, in the coordinates of the frame.</param>
    /// <param name="box">The width and the height of the box the text is laid out into. Zero means as large as the text.</param>
    /// <param name="text">The text to draw.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
    /// <remarks>
    /// A text that says nothing, or whose box holds no line at all, draws nothing and reports a measured size of zero,
    /// which is what a caller that sizes a panel to its text needs to know.
    /// </remarks>
    public void Draw(World world, Entity entity, Vector2 position, Vector2 box, in TextComponent text)
    {
        ArgumentNullException.ThrowIfNull(world);

        LaidOut laid = Laid(entity, box, text);
        ref TextComponent component = ref world.GetRef<TextComponent>(entity);
        component.Font = laid.Font;
        component.MeasuredSize = laid.Layout.Size;

        foreach (TextLine line in laid.Layout.Lines)
        {
            DrawLine(laid.Measurer, line, position + line.Position, text.Style.Color);
        }
    }

    /// <summary>Gets the height of a line of the fonts that this renderer draws with.</summary>
    /// <remarks>
    /// A caller that stacks lines of its own, such as a console or a list of numbers, advances by this. Reading it bakes the
    /// fonts that the defaults name, which the service then answers from its cache.
    /// </remarks>
    public float LineHeight => Measurer(new TextStyle()).Measurer.Metrics.LineHeight;

    /// <summary>Draws a text that no entity carries, such as a line of a console or of the numbers of a frame.</summary>
    /// <param name="text">The text to draw. A leave of null or an empty text draws nothing and takes no room.</param>
    /// <param name="position">The top-left corner of the box that the text is laid out into.</param>
    /// <param name="style">How the text is written, which says which fonts draw it and in which colour.</param>
    /// <param name="box">The width and the height of the box, where a side of zero means as large as the text.</param>
    /// <returns>The size that the text takes, which is what a caller that stacks lines advances by.</returns>
    /// <remarks>
    /// A game holds strings that no entity carries: a console, the numbers of a frame, a line it built itself. The text is
    /// laid out once for as long as it, its style, its box and the language of the game do not change, so a caller that draws
    /// the same line on every frame measures it once, and the box is what a caller that has one wraps the text into.
    /// </remarks>
    /// <example>
    /// <code>
    /// float line = text.LineHeight;
    ///
    /// for (int index = 0; index &lt; lines.Count; index++)
    /// {
    ///     text.Draw(lines[index], new Vector2(8f, 8f + (index * line)), new TextStyle { Color = Color.White });
    /// }
    /// </code>
    /// </example>
    public Vector2 Draw(string? text, Vector2 position, in TextStyle style, Vector2 box = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        Cached cached = Laid(text, box, style);

        foreach (TextLine line in cached.Layout.Lines)
        {
            DrawLine(cached.Measurer, line, position + line.Position, style.Color);
        }

        return cached.Layout.Size;
    }

    /// <summary>Returns the layout of the text of an entity, laying it out when something about it changed.</summary>
    /// <param name="entity">The entity that carries the text.</param>
    /// <param name="box">The box the text is laid out into.</param>
    /// <param name="text">The text to lay out.</param>
    /// <returns>The layout and everything that was resolved to build it.</returns>
    private LaidOut Laid(Entity entity, Vector2 box, in TextComponent text)
    {
        string language = _text.Language;

        if (_remembered.TryGetValue(entity, out LaidOut? cached) && cached.Matches(text, box, language))
        {
            return cached;
        }

        // A world that holds more texts than are remembered starts over rather than growing without a bound: a text that is
        // drawn again is laid out again, which costs a measurement each.
        if (_remembered.Count >= MaximumRemembered)
        {
            _remembered.Clear();
        }

        (ITextMeasurer measurer, FontHandle font) = Measurer(text.Style);
        string resolved = Resolve(text);
        var laid = new LaidOut(
            text.Key,
            text.Count,
            text.Text,
            language,
            box,
            text.Style,
            resolved,
            measurer,
            font,
            TextLayouter.Layout(resolved, measurer, text.Style, box));

        _remembered[entity] = laid;
        return laid;
    }

    /// <summary>Returns the layout of a text that no entity carries, laying it out when something about it changed.</summary>
    /// <param name="text">The text to lay out, which is what the layout is remembered under.</param>
    /// <param name="box">The box the text is laid out into.</param>
    /// <param name="style">The style the text is laid out with.</param>
    /// <returns>The layout and everything that was resolved to build it.</returns>
    private Cached Laid(string text, Vector2 box, in TextStyle style)
    {
        string language = _text.Language;

        if (_lines.TryGetValue(text, out Cached? cached) && cached.Matches(box, style, language))
        {
            return cached;
        }

        if (_lines.Count >= MaximumRemembered)
        {
            _lines.Clear();
        }

        (ITextMeasurer measurer, _) = Measurer(style);
        var laid = new Cached(box, style, language, text, measurer, TextLayouter.Layout(text, measurer, style, box));

        _lines[text] = laid;
        return laid;
    }

    /// <summary>Returns the string of a text: what its key says, or the text it holds when it names no key.</summary>
    /// <param name="text">The text to resolve.</param>
    /// <returns>The characters to lay out, which is an empty string for a text that says nothing.</returns>
    private string Resolve(in TextComponent text)
    {
        if (text.Key is string key && !string.IsNullOrWhiteSpace(key))
        {
            return _text.Resolve(key, ("count", text.Count));
        }

        return text.Text ?? string.Empty;
    }

    /// <summary>Returns the measurer of a style, baking the fonts of its stack.</summary>
    /// <param name="style">The style whose fonts are baked.</param>
    /// <returns>The measurer of the text and the font that draws its first line, which is a default handle for the built-in font.</returns>
    /// <remarks>
    /// A style that names no font is drawn with the defaults of this renderer, which is what makes a text of a game readable
    /// without the game naming a font, and a font that cannot be baked is reported once and left out of the stack, so the
    /// characters it covers fall to the font below it, or to the built-in font when nothing is left.
    /// </remarks>
    private (ITextMeasurer Measurer, FontHandle Font) Measurer(in TextStyle style)
    {
        FontStyle[] fonts = style.Fonts is { Length: > 0 } named ? named : _defaults;

        if (_fonts is null || fonts.Length == 0)
        {
            return (BitmapTextMeasurer.Instance, default);
        }

        var stack = new List<(FontStyle Style, FontHandle Font)>(fonts.Length);

        foreach (FontStyle font in fonts)
        {
            if (font.Path is not string path || font.PixelHeight <= 0f)
            {
                continue;
            }

            try
            {
                stack.Add((font, _fonts.Load(path, font.PixelHeight, font.First, font.Last)));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
            {
                Report(path, exception);
            }
        }

        return stack.Count == 0
            ? (BitmapTextMeasurer.Instance, default)
            : (new StackedTextMeasurer(_fonts, stack), stack[0].Font);
    }

    /// <summary>Draws one line of a laid out text, in runs of one font each.</summary>
    /// <param name="measurer">The measurement of the fonts of the text.</param>
    /// <param name="line">The line to draw.</param>
    /// <param name="position">The top-left corner of the line.</param>
    /// <param name="color">The color of the glyphs.</param>
    /// <remarks>A line that mixes the scripts of two languages is drawn in one run per font, so what is drawn is what the measurer measured.</remarks>
    private void DrawLine(ITextMeasurer measurer, in TextLine line, Vector2 position, Color color)
    {
        float pen = position.X;
        int start = 0;
        FontHandle? font = line.Text.Length > 0 ? measurer.Font(line.Text[0]) : null;

        for (var index = 1; index <= line.Text.Length; index++)
        {
            FontHandle? next = index < line.Text.Length ? measurer.Font(line.Text[index]) : null;

            if (index < line.Text.Length && next == font)
            {
                continue;
            }

            string run = line.Text[start..index];
            DrawRun(run, new Vector2(pen, position.Y), color, font);
            pen += measurer.Measure(run).X;
            start = index;
            font = next;
        }
    }

    /// <summary>Draws a run of characters that one font covers, or the built-in font when the run names none.</summary>
    /// <param name="run">The characters to draw.</param>
    /// <param name="position">The top-left corner of the run.</param>
    /// <param name="color">The color of the glyphs.</param>
    /// <param name="font">The font that covers the run, or null for the built-in font.</param>
    private void DrawRun(string run, Vector2 position, Color color, FontHandle? font)
    {
        if (font is FontHandle handle && _fonts is not null)
        {
            _fonts.Draw(handle, run, position, color);
            return;
        }

        _renderer.DrawText(run, position, color);
    }

    /// <summary>Reports a font of a style that cannot be baked, once per path.</summary>
    /// <param name="path">The path of the font that was asked for.</param>
    /// <param name="exception">What stopped the bake.</param>
    private void Report(string path, Exception exception)
    {
        if (_logger is not null && _reported.Add(path))
        {
            _logger.LogError("The font '{Path}' of a text cannot be baked, so the characters it covers are drawn by the font below it in the stack, or by the built-in font: {Reason}", path, exception.Message);
        }
    }

    /// <summary>Returns a value indicating whether two styles say the same thing.</summary>
    /// <param name="left">The style that was laid out.</param>
    /// <param name="right">The style of the text as it is now.</param>
    /// <returns><see langword="true"/> when the layout of one style fits the other.</returns>
    private static bool SameStyle(in TextStyle left, in TextStyle right) =>
        left.Color == right.Color && left.Align == right.Align && left.VerticalAlign == right.VerticalAlign &&
        left.Wrap == right.Wrap && left.Overflow == right.Overflow && left.LineSpacing == right.LineSpacing &&
        string.Equals(left.Ellipsis, right.Ellipsis, StringComparison.Ordinal) && SameFonts(left.Fonts, right.Fonts);

    /// <summary>Returns a value indicating whether two stacks name the same fonts, in the same order.</summary>
    /// <param name="left">The stack that was laid out.</param>
    /// <param name="right">The stack of the style as it is now.</param>
    /// <returns><see langword="true"/> when the two hold the same fonts in the same order.</returns>
    private static bool SameFonts(FontStyle[]? left, FontStyle[]? right) =>
        ReferenceEquals(left, right) || (left is not null && right is not null && left.AsSpan().SequenceEqual(right));

    /// <summary>A text that no entity carries, as it was laid out.</summary>
    /// <param name="Box">The box the text was laid out into.</param>
    /// <param name="Style">The style the text was laid out with.</param>
    /// <param name="Language">The language of the game when the text was laid out.</param>
    /// <param name="Resolved">The characters that were laid out.</param>
    /// <param name="Measurer">The measurement of the fonts of the text.</param>
    /// <param name="Layout">The lines of the text and their places.</param>
    private sealed record Cached(
        Vector2 Box,
        TextStyle Style,
        string Language,
        string Resolved,
        ITextMeasurer Measurer,
        TextLayout Layout)
    {
        /// <summary>Returns a value indicating whether the layout that was remembered fits the text as it is now.</summary>
        /// <param name="box">The box the text is laid out into now.</param>
        /// <param name="style">The style of the text as it is now.</param>
        /// <param name="language">The language of the game now, which is what a text of a key says in.</param>
        /// <returns><see langword="true"/> when the layout that was remembered fits the text as it is now.</returns>
        public bool Matches(Vector2 box, in TextStyle style, string language) =>
            string.Equals(Language, language, StringComparison.Ordinal) && Box == box && SameStyle(Style, style);
    }

    /// <summary>The text of an entity as it was laid out, with everything that was resolved for it.</summary>
    private sealed record LaidOut(
        string? Key,
        int Count,
        string? Text,
        string Language,
        Vector2 Box,
        TextStyle Style,
        string Resolved,
        ITextMeasurer Measurer,
        FontHandle Font,
        TextLayout Layout)
    {
        /// <summary>Returns a value indicating whether the layout that was remembered fits the text as it is now.</summary>
        /// <param name="text">The text of the entity as it is now.</param>
        /// <param name="box">The box the text is laid out into now.</param>
        /// <param name="language">The language of the game now, which is what a text of a key says in.</param>
        /// <returns><see langword="true"/> when the layout that was remembered fits the text as it is now.</returns>
        public bool Matches(in TextComponent text, Vector2 box, string language) =>
            string.Equals(Key, text.Key, StringComparison.Ordinal) &&
            Count == text.Count &&
            string.Equals(Text, text.Text, StringComparison.Ordinal) &&
            string.Equals(Language, language, StringComparison.Ordinal) &&
            Box == box &&
            SameStyle(Style, text.Style);
    }
}
