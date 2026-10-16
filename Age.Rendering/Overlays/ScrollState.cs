using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Where a list of rows is scrolled, which is what every page of the developer window that is longer than its body holds.
/// </summary>
/// <remarks>
/// <para>
/// A list of rows is longer than the body of a page more often than not — a console of an hour of play, a world of a
/// thousand entities — and what a person reads is a window of it rather than the whole. The state holds the row that is
/// drawn first and follows a drag of the bar at the right edge of the body: what a page draws is a window of the rows it
/// built, and the bar beside it says where a person stands in them and is what a person drags.
/// </para>
/// <para>
/// The drag is followed from the first press to the release of the button, whatever the pointer does between them: a
/// person who drags the bar off the top or the bottom of the body keeps the drag rather than losing it, which is what
/// a scrollbar of a list of a thousand rows needs.
/// </para>
/// </remarks>
public struct ScrollState
{
    private const float BarWidth = 4f;
    private const float BarPad = 2f;
    private const float MinimumThumbHeight = 12f;

    private int _first;
    private bool _dragging;
    private float _dragOffset;

    /// <summary>Gets the row that is drawn first, counting from zero.</summary>
    public readonly int First => _first;

    /// <summary>Gets a value indicating whether the rows that are built are longer than the room of the body.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets a value indicating whether the bar is being dragged, which is what a page reads to keep following a drag of its own.</summary>
    public readonly bool Dragging => _dragging;

    /// <summary>Reads the pointer of a frame: the press that begins a drag and the button that ends it.</summary>
    /// <param name="pointer">The pointer of the frame, which carries the state of the button.</param>
    /// <param name="body">The rectangle the page owns, in the pixels of the window.</param>
    /// <param name="rows">The number of rows the page built.</param>
    /// <param name="line">The height of one row, in pixels.</param>
    /// <remarks>
    /// A drag is followed whether the pointer stays in the body or not, because the button is what the drag follows rather
    /// than the rectangle it began in.
    /// </remarks>
    public void Update(in WindowPointer pointer, Rect body, int rows, float line)
    {
        int visible = Visible(body, line);
        IsActive = rows > visible;

        // The drag ends on the frame the button comes up, which is what a scrollbar that keeps following the pointer after
        // the button is released would otherwise do: the row that was under the pointer would be dragged with it.
        if (!pointer.Down)
        {
            _dragging = false;
        }

        if (IsActive)
        {
            UpdateDrag(pointer, body, rows, visible);
        }

        _first = Math.Clamp(_first, 0, Math.Max(0, rows - visible));
    }

    /// <summary>Returns the number of rows that the body shows.</summary>
    /// <param name="body">The rectangle the page owns.</param>
    /// <param name="line">The height of one row, in pixels.</param>
    public static int Visible(Rect body, float line) => line <= 0f ? 0 : Math.Max(1, (int)(body.Height / line));

    /// <summary>Moves the row that is drawn first to its end, which a page that follows the newest row does.</summary>
    /// <param name="body">The rectangle the page owns.</param>
    /// <param name="rows">The number of rows the page built.</param>
    /// <param name="line">The height of one row, in pixels.</param>
    public void ToEnd(Rect body, int rows, float line) => _first = Math.Max(0, rows - Visible(body, line));

    /// <summary>Draws the track and the thumb of the scrollbar at the right edge of the body.</summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="body">The rectangle the page owns.</param>
    /// <param name="rows">The number of rows the page built.</param>
    /// <param name="line">The height of one row, in pixels.</param>
    /// <remarks>A page that shows every row it built draws no bar, which is what a list that fits its body looks like.</remarks>
    public readonly void DrawBar(IRenderer renderer, Rect body, int rows, float line)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        int visible = Visible(body, line);

        if (rows <= visible || visible == 0)
        {
            return;
        }

        float x = BarX(body);
        renderer.DrawRectangle(new Rect(new Vector2(x, body.Y), new Vector2(BarWidth, body.Height)), new Color(30, 30, 38));

        float height = ThumbHeight(body, rows, visible);
        float y = ThumbTop(body, rows, visible, height);

        renderer.DrawRectangle(new Rect(new Vector2(x, y), new Vector2(BarWidth, height)), new Color(96, 96, 118));
    }

    /// <summary>Begins a drag of the bar, or follows the one that is going on, and moves the row that is drawn first.</summary>
    /// <param name="pointer">The pointer of the frame.</param>
    /// <param name="body">The rectangle the page owns.</param>
    /// <param name="rows">The number of rows the page built.</param>
    /// <param name="visible">The number of rows the body shows.</param>
    /// <remarks>
    /// A press that lands on the bar is a press on the scrollbar, which is what the page reads to begin a drag: a press
    /// that lands on the thumb keeps the offset of the pointer inside it, and one that lands on the track beside it
    /// centres the thumb under the pointer, which is what a scrollbar of an operating system does.
    /// </remarks>
    private void UpdateDrag(in WindowPointer pointer, Rect body, int rows, int visible)
    {
        if (_dragging)
        {
            ApplyDrag(pointer, body, rows, visible);
            return;
        }

        if (!pointer.Pressed)
        {
            return;
        }

        float x = BarX(body);

        if (pointer.Position.X < x || pointer.Position.X > x + BarWidth)
        {
            return;
        }

        float height = ThumbHeight(body, rows, visible);
        float top = ThumbTop(body, rows, visible, height);

        _dragOffset = pointer.Position.Y >= top && pointer.Position.Y <= top + height
            ? pointer.Position.Y - top
            : height * 0.5f;

        _dragging = true;
        ApplyDrag(pointer, body, rows, visible);
    }

    /// <summary>Writes the row that is drawn first from where the pointer stands, which a drag of the bar is.</summary>
    private void ApplyDrag(in WindowPointer pointer, Rect body, int rows, int visible)
    {
        float height = ThumbHeight(body, rows, visible);
        float track = body.Height - height;

        if (track <= 0f)
        {
            _first = 0;
            return;
        }

        float y = pointer.Position.Y - body.Y - _dragOffset;
        float share = Math.Clamp(y / track, 0f, 1f);

        int maximum = Math.Max(0, rows - visible);
        _first = (int)MathF.Round(share * maximum);
    }

    /// <summary>Returns the height of the thumb, which is the share of the body that the rows in view take.</summary>
    private static float ThumbHeight(Rect body, int rows, int visible)
    {
        float share = Math.Clamp(visible / (float)rows, 0.05f, 1f);
        return MathF.Max(MinimumThumbHeight, body.Height * share);
    }

    /// <summary>Returns the top of the thumb, which is where the rows in view begin in the whole list.</summary>
    private readonly float ThumbTop(Rect body, int rows, int visible, float height)
    {
        int maximum = Math.Max(1, rows - visible);
        float track = body.Height - height;

        return body.Y + (track * (_first / (float)maximum));
    }

    /// <summary>Returns the left edge of the bar, which is what a press on the scrollbar is tested against.</summary>
    private static float BarX(Rect body) => body.X + body.Width - BarWidth - BarPad;
}
