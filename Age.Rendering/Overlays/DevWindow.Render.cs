using Age.Core;

namespace Age.Rendering;

/// <summary>Draws the developer window, which is the half of it that is not reading keys.</summary>
public sealed partial class DevWindow
{
    /// <summary>Draws the window over the frame: the panel, the title bar, the row of tabs and the page of the tab that is shown.</summary>
    /// <param name="world">The world of the frame, which the window does not read: it reports the engine rather than the game.</param>
    /// <param name="camera">The camera of the frame, which the window does not read either, because it draws in the pixels of the window.</param>
    /// <remarks>
    /// Every draw of the window lands inside it: the panel, the title bar and the tabs are drawn in the frame of the window, and the
    /// page is drawn inside a clip of the body, so a tab that draws past its edge lands nowhere rather than over the game.
    /// </remarks>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsOpen)
        {
            return;
        }

        Rect window = WindowRect(_renderer.ViewportSize);
        Rect body = BodyRect(window);

        _renderer.BeginFrame(false);

        _renderer.DrawRectangle(window, PanelColour);
        _renderer.DrawRectangle(new Rect(window.Position, new Vector2(window.Width, TitleHeight)), TitleColour);
        _renderer.DrawRectangle(new Rect(new Vector2(window.X, window.Y + TitleHeight), new Vector2(window.Width, 1f)), BorderColour);

        Write(ResolveTitle(), new Vector2(window.X + Padding, window.Y + 5f), TitleTextColour);

        // The cross of the window stands at the right end of the title bar, which is what closes the whole window.
        DrawCross(CloseRect(window.X + window.Width - TitleHeight, window.Y));

        float x = window.X;

        for (var index = 0; index < _tabs.Count; index++)
        {
            IDevWindowTab tab = _tabs[index];
            float width = TabWidthOf(tab);
            var label = new Rect(new Vector2(x, window.Y + TitleHeight), new Vector2(width, TabHeight));

            _renderer.DrawRectangle(label, index == _active ? ActiveTabColour : InactiveTabColour);
            Write(tab.Title, new Vector2(x + Padding, window.Y + TitleHeight + 5f), TabTextColour);

            // A tab that may be closed carries its own cross, which takes the click that closes just that page rather than the window.
            if (tab.Closable)
            {
                DrawCross(CloseRect(x + width - TabHeight, window.Y + TitleHeight));
            }

            x += width;
        }

        _renderer.DrawRectangle(new Rect(new Vector2(window.X, window.Y + TitleHeight + TabHeight), new Vector2(window.Width, 1f)), BorderColour);

        // The page is drawn inside a clip, so a tab that draws past the edge of the body lands nowhere rather than over the game: it
        // is what a scrollable page and a long list of the game both rely on.
        if (_tabs.Count > 0)
        {
            _renderer.PushClip(body);
            _tabs[_active].Render(_renderer, body);
            _renderer.PopClip();
        }

        _renderer.EndFrame();
    }

    /// <summary>Returns the width of a tab, which its label and its cross make wider than the least a tab is drawn at.</summary>
    /// <param name="tab">The tab whose label is measured.</param>
    private float TabWidthOf(IDevWindowTab tab) => Math.Max(TabWidth, Measure(tab.Title).X + (2f * Padding) + (tab.Closable ? TabHeight : 0f));

    /// <summary>Draws the cross of a bar, which is what closes a tab or the window.</summary>
    /// <param name="rect">The square the cross stands in.</param>
    /// <remarks>
    /// The cross is two thin bars rather than two lines, because the renderer draws rectangles and no diagonals: what a person sees
    /// is the same X as the one on any window of a desktop, and the colour turns when the pointer is over it, which is what makes it
    /// read as a button before it is pressed.
    /// </remarks>
    private void DrawCross(Rect rect)
    {
        Color colour = Contains(rect, _input.MousePosition) ? CloseHotColour : CloseColour;
        const float Thickness = 2f;
        float side = rect.Width;
        int steps = (int)(side / Thickness);

        for (var step = 0; step <= steps; step++)
        {
            float offset = step * (side - Thickness) / steps;

            _renderer.DrawRectangle(new Rect(new Vector2(rect.X + offset, rect.Y + offset), new Vector2(Thickness, Thickness)), colour);
            _renderer.DrawRectangle(new Rect(new Vector2(rect.X + side - Thickness - offset, rect.Y + offset), new Vector2(Thickness, Thickness)), colour);
        }
    }

    /// <summary>Draws a line of the window with the fonts of the engine, or with the built-in font when it has none.</summary>
    /// <param name="text">The characters of the line.</param>
    /// <param name="position">The top-left corner of the line.</param>
    /// <param name="color">The colour of the glyphs.</param>
    private void Write(string text, Vector2 position, Color color)
    {
        if (_textRenderer is null)
        {
            _renderer.DrawText(text, position, color);
            return;
        }

        _textRenderer.Draw(text, position, new TextStyle { Color = color });
    }

    /// <summary>Returns the size a line takes, which is what the label of a tab is measured with.</summary>
    /// <param name="text">The line to measure.</param>
    private Vector2 Measure(string text)
    {
        if (_textRenderer is null)
        {
            return new Vector2(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);
        }

        return _textRenderer.Measure(text, new TextStyle());
    }

    /// <summary>Returns the title of the window in the language of the game, or the key itself when the game ships no string for it.</summary>
    private string ResolveTitle() => _textSource?.Resolve("ui-dev-window-title") ?? "ui-dev-window-title";
}
