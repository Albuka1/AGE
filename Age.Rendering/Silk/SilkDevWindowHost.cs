using Age.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// A developer-window host backed by a second Silk.NET window: a window of the operating system, beside the game, with a context of
/// its own.
/// </summary>
/// <remarks>
/// <para>
/// The window is created the first time it is asked for and owns a renderer of its own, because a context belongs to the window it
/// was made from: what this draws goes to this window and nothing of it needs the context of the game. That is what makes the host a
/// passthrough — the rectangle and the text a page asks for are drawn here — rather than a second renderer of the world.
/// </para>
/// <para>
/// The pointer and the clicks come from the input context of this window, so a click on a tab is a click in this window and never one
/// the game reads: the two windows have their own input, which is what a window of the operating system is.
/// </para>
/// </remarks>
public sealed class SilkDevWindowHost : IDevWindowHost
{
    private IWindow? _window;
    private SilkRenderer? _renderer;
    private IInputContext? _input;
    private IMouse? _mouse;
    private Vector2? _clicked;
    private bool _clickReported;
    private Vector2 _pointer;
    private bool _down;

    /// <inheritdoc />
    public bool IsOpen => _window is { IsClosing: false };

    /// <inheritdoc />
    public Vector2 Size => _window is IWindow window
        ? new Vector2(window.Size.X, window.Size.Y)
        : Vector2.Zero;

    /// <inheritdoc />
    public Vector2 Pointer => _pointer;

    /// <inheritdoc />
    public bool PointerDown => _down;

    /// <inheritdoc />
    public void Create(int width, int height, string title)
    {
        if (_window is not null)
        {
            return;
        }

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = title,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3)),

            // The window stands beside the game rather than taking its place, and it is drawn on the frame the game pumps it rather
            // than on a loop of its own. A window that is driven rather than event-driven is what keeps it from waiting for events of
            // its own: an event-driven window blocks in DoEvents until one arrives, and two of them on one thread is what hangs the
            // game, because the events of every window arrive through the one platform of the process.
            IsEventDriven = false,
            IsVisible = true,
            VSync = false,
        };

        _window = Window.Create(options);
        _window.Initialize();

        _renderer = new SilkRenderer();
        _renderer.Attach(new HostWindowService(_window));

        _input = _window.CreateInput();
        _mouse = _input.Mice.Count > 0 ? _input.Mice[0] : null;

        if (_mouse is not null)
        {
            _mouse.MouseDown += OnMouseDown;
            _mouse.MouseUp += OnMouseUp;
        }
    }

    /// <inheritdoc />
    public bool Pump(out Vector2? clicked)
    {
        if (_window is not IWindow window || window.IsClosing)
        {
            clicked = null;
            return false;
        }

        // The events of this window are read here rather than by the loop of the game, because it is a window of its own: what the
        // window manager sends it, such as a move or a close, arrives while the game runs its frame.
        window.DoEvents();

        if (_mouse is not null)
        {
            _pointer = new Vector2(_mouse.Position.X, _mouse.Position.Y);
        }

        clicked = _clickReported ? _clicked : null;
        _clickReported = false;
        _clicked = null;

        return !window.IsClosing;
    }

    /// <inheritdoc />
    public void BeginFrame(bool clear)
    {
        _window?.GLContext?.MakeCurrent();
        _renderer?.BeginFrame(clear);
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        _renderer?.EndFrame();
        _window?.SwapBuffers();
    }

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color) => _renderer?.DrawRectangle(rect, color);

    /// <inheritdoc />
    public void DrawText(string text, Vector2 position, Color color) => _renderer?.DrawText(text, position, color);

    /// <inheritdoc />
    /// <remarks>Text is measured with the built-in font, which is what the host draws its text with, so a label is measured by what draws it.</remarks>
    public Vector2 Measure(string text) => new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_mouse is not null)
        {
            _mouse.MouseDown -= OnMouseDown;
            _mouse.MouseUp -= OnMouseUp;
        }

        _input?.Dispose();
        _input = null;
        _mouse = null;

        _renderer?.Dispose();
        _renderer = null;

        _window?.Close();
        _window?.Dispose();
        _window = null;
    }

    /// <summary>Records that the left button of this window went down, which is what a click on a tab or a cross is.</summary>
    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _clicked = new Vector2(mouse.Position.X, mouse.Position.Y);
            _clickReported = true;
            _down = true;
        }
    }

    /// <summary>Records that the left button of this window came up, which is what the end of a drag of a selection is.</summary>
    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _down = false;
        }
    }

    /// <summary>Presents a window of the operating system as the <see cref="IWindowService"/> the renderer attaches to, so the renderer makes its context current and measures the right surface.</summary>
    private sealed class HostWindowService(IWindow window) : IWindowService
    {
        public IWindow Window { get; } = window;

        public void Create(int width, int height, string title) => throw new NotSupportedException("This window service presents a window that already exists.");

        public void SetIcon(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException("This window service presents a window that already exists.");

        public void Close() => Window.Close();
    }
}
