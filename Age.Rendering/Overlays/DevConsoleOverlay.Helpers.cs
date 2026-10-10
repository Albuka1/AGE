using Age.Core;

namespace Age.Rendering;

/// <summary>The parts of the console panel that are not the drawing of a frame: the slide, the suggestions and the measuring.</summary>
public sealed partial class DevConsoleOverlay
{
    /// <summary>Returns how far the panel has slid into the frame, where one is fully in and zero is fully out.</summary>
    /// <remarks>
    /// The panel is in at once when the animation is turned off, and a closing panel is measured from the moment it started to
    /// close rather than from the start of the game loop, so a console that is opened and closed often still slides.
    /// </remarks>
    private float Slide()
    {
        if (!_console.IsOpen && !_closing)
        {
            return 0f;
        }

        if (AnimationDuration <= 0f)
        {
            return _console.IsOpen ? 1f : 0f;
        }

        if (!_closing)
        {
            return Math.Clamp((float)((_frameTime - _openedAt) / AnimationDuration), 0f, 1f);
        }

        float closing = Math.Clamp(1f - (float)((_frameTime - _closedAt) / AnimationDuration), 0f, 1f);

        if (closing <= 0f)
        {
            _closing = false;
        }

        return closing;
    }

    /// <summary>Returns the commands that match what is being typed, which is what the panel lists under the input.</summary>
    private IReadOnlyList<ConsoleCommand> Suggestions()
    {
        // Only the first word of a line is a name: a line that already holds arguments is naming something of the game, and the
        // rows of the panel would then cover the output for nothing.
        string line = _console.Input;

        return line.AsSpan().IndexOfAny(' ', '\t') >= 0
            ? []
            : [.. _console.Matches(line).Take(_visibleSuggestions)];
    }

    /// <summary>Takes the chosen suggestion into the line being typed, which is what Tab does while the rows are listed.</summary>
    /// <returns><see langword="true"/> when a suggestion was taken, <see langword="false"/> when there is nothing to take.</returns>
    private bool TakeSuggestion()
    {
        IReadOnlyList<ConsoleCommand> suggestions = Suggestions();

        if (suggestions.Count == 0)
        {
            return false;
        }

        int index = _suggested < 0 ? 0 : Math.Min(_suggested, suggestions.Count - 1);
        _console.SetInput(suggestions[index].Name);
        _suggested = -1;

        return true;
    }

    /// <summary>Moves the chosen row of the suggestions by a step and wraps around its ends.</summary>
    /// <param name="step">The step to move by, where one walks down the list and minus one walks up it.</param>
    /// <returns><see langword="true"/> when the list is there and the choice moved, so the key is not the history of the console.</returns>
    private bool MoveSuggestion(int step)
    {
        int count = Suggestions().Count;

        if (count == 0)
        {
            return false;
        }

        _suggested = (((_suggested + step) % count) + count) % count;
        return true;
    }

    /// <summary>Draws a line of the panel with the fonts of the game, or with the built-in font when it has none.</summary>
    /// <returns>The size the line takes, whose width is what places the caret and the description of a row.</returns>
    private Vector2 Write(string text, Vector2 position, Color color)
    {
        if (_textRenderer is null)
        {
            _renderer.DrawText(text, position, color);

            // The built-in font is a bitmap whose glyphs are all the same width, so a line is measured rather than laid out.
            return new Vector2(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);
        }

        return _textRenderer.Draw(text, position, new TextStyle { Color = color });
    }

    /// <summary>Returns the title of the panel in the language of the game, or the key itself when the game ships no string for it.</summary>
    private string ResolveTitle() => _textSource?.Resolve("console-title") ?? "console-title";

    /// <summary>Returns the colour a line of the output is drawn in, which is what makes a failure and a warning stand out.</summary>
    private static Color ColourOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Error => ErrorColour,
        ConsoleLevel.Warning => WarningColour,
        _ => TextColour,
    };
}
