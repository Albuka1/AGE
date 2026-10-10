using Age.Core;

namespace Age.Rendering;

/// <summary>
/// A developer-window host that opens nothing, which is what a run with no window has: the pages are kept and nothing is drawn.
/// </summary>
/// <remarks>
/// A headless run, a test and a server have no window of the operating system to open, and a game that asks for a developer window
/// is not a game that needs one. This answers every call without a window, so the service keeps its pages and its state and a caller
/// never has to ask whether the window is really there.
/// </remarks>
public sealed class NullDevWindowHost : IDevWindowHost
{
    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public Vector2 Size { get; private set; }

    /// <inheritdoc />
    /// <remarks>The pointer of a window that is never there does not exist, so it is nowhere and not held.</remarks>
    public Vector2 Pointer => Vector2.Zero;

    /// <inheritdoc />
    public bool PointerDown => false;

    /// <inheritdoc />
    public void Create(int width, int height, string title)
    {
        Size = new Vector2(width, height);
        IsOpen = true;
    }

    /// <inheritdoc />
    /// <remarks>A window that draws nothing reads no event of the operating system, so no click is ever reported.</remarks>
    public bool Pump(out Vector2? clicked)
    {
        clicked = null;
        return IsOpen;
    }

    /// <inheritdoc />
    public void BeginFrame(bool clear)
    {
    }

    /// <inheritdoc />
    public void EndFrame()
    {
    }

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color)
    {
    }

    /// <inheritdoc />
    public void DrawText(string text, Vector2 position, Color color)
    {
    }

    /// <inheritdoc />
    /// <remarks>Text is measured with the built-in font, whose glyphs are all the same width, which is what a window that draws nothing would have drawn with.</remarks>
    public Vector2 Measure(string text) => new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);

    /// <inheritdoc />
    /// <remarks>A window that draws nothing has nothing to keep inside a rectangle, so a push is ignored.</remarks>
    public void PushClip(Rect rect)
    {
    }

    /// <inheritdoc />
    public void PopClip()
    {
    }

    /// <inheritdoc />
    public void Dispose() => IsOpen = false;
}
