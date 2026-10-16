using Age.Core;
using Age.Input;
using Silk.NET.GLFW;
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
/// The pointer and the click are read through GLFW directly rather than through the input context of Silk.NET, because the platform
/// of the engine answers the input of one window and a second context on the same thread collides with it. The button is the one
/// state that is asked of the input of the game when the game has one: a release that lands outside this window is an event this
/// window never receives, and a state that read its own events alone would hold the button down for the rest of the run. The API is
/// the one Silk.NET itself holds — <c>GlfwProvider.GLFW.Value</c> — because a second call to <c>Glfw.GetApi</c> hands back an object
/// that is not the one the window was created with, and a callback set through it is never invoked.
/// </para>
/// </remarks>
public sealed unsafe class SilkDevWindowHost : IDevWindowHost
{
    private readonly IWindowService? _game;
    private readonly IInputService? _input;

    private IWindow? _window;
    private SilkRenderer? _renderer;
    private Glfw? _glfw;
    private WindowHandle* _handle;
    private GlfwCallbacks.MouseButtonCallback? _mouseButtonCallback;
    private GlfwCallbacks.CursorEnterCallback? _cursorEnterCallback;
    private Vector2? _clicked;
    private bool _clickReported;
    private Vector2 _cursor;
    private bool _down;
    private bool _focused;

    /// <summary>Initializes the host with the window of the game and the input of the game, which its button state is read from.</summary>
    /// <param name="game">
    /// The window of the game, which this window borrows the current context from: the two windows share one thread, so a frame of
    /// this window makes its context current and the window of the game is made current again afterwards. A host with no game window
    /// leaves whatever context was current alone, which is what a headless run and a test have.
    /// </param>
    /// <param name="input">
    /// The input of the game, whose mouse is bound to the window of the game and answers the physical state of the pointer: a
    /// release that lands outside this window is a release this window never hears about, and a state that read its own events
    /// alone would hold the button down for the rest of the run. A host that was handed none falls back to what its own handle
    /// reports, which is what a headless run and a test have.
    /// </param>
    public SilkDevWindowHost(IWindowService? game = null, IInputService? input = null)
    {
        _game = game;
        _input = input;
    }

    /// <inheritdoc />
    public bool IsOpen => _window is { IsClosing: false };

    /// <inheritdoc />
    /// <remarks>
    /// The focus is tracked from the events of the window rather than asked of the platform on every frame: the window manager
    /// reports that the focus arrived or went, and what a game reads between two of those reports does not change.
    /// </remarks>
    public bool Focused => _focused;

    /// <inheritdoc />
    public Vector2 Size => _window is IWindow window
        ? new Vector2(window.Size.X, window.Size.Y)
        : Vector2.Zero;

    /// <inheritdoc />
    public Vector2 Pointer => _cursor;

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

        _window.FocusChanged += OnFocusChanged;

        _renderer = new SilkRenderer();
        _renderer.Attach(new HostWindowService(_window));

        // The API of the platform is the one Silk.NET itself created the window with: a second call to `Glfw.GetApi` hands back
        // an object of its own, and a callback set through it names a handle the platform has never seen. The provider holds the
        // instance that was used to create this window, so a callback set through it is the callback of this window.
        _glfw = GlfwProvider.GLFW.Value;

        nint native = _window.Native?.Glfw ?? 0;

        if (native != 0)
        {
            _handle = (WindowHandle*)native;
            _mouseButtonCallback = OnMouseButton;
            _cursorEnterCallback = OnCursorEnter;

            _glfw.SetMouseButtonCallback(_handle, _mouseButtonCallback);
            _glfw.SetCursorEnterCallback(_handle, _cursorEnterCallback);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The cursor is asked of this window on every frame, which is what the position of the pointer is. The button is asked of the
    /// input of the game when there is one, because the physical state of a button is what a drag of a scrollbar reads: a release
    /// that lands outside this window never reaches it as an event, and the window would hold the button down for the rest of the
    /// run.
    /// </remarks>
    public bool Pump(out Vector2? clicked)
    {
        if (_window is not IWindow window || window.IsClosing)
        {
            clicked = null;
            return false;
        }

        window.DoEvents();

        if (_glfw is not null && _handle is not null)
        {
            _glfw.GetCursorPos(_handle, out double x, out double y);
            _cursor = new Vector2((float)x, (float)y);
        }

        // The input of the game is the one place that answers the physical state of the pointer for the whole process: a release
        // that lands outside this window is a release this window never hears about, and a state that read its own events alone
        // would hold the button down for the rest of the run. A host that was handed no input falls back to its own handle, which
        // is what a headless run and a test have.
        if (_input is not null)
        {
            _down = _input.IsMouseButtonDown(Silk.NET.Input.MouseButton.Left);
        }
        else if (_glfw is not null && _handle is not null)
        {
            _down = _glfw.GetMouseButton(_handle, (int)Silk.NET.GLFW.MouseButton.Left) == (int)InputAction.Press;
        }
        else
        {
            _down = false;
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

        // The two windows share one thread and one device, so this frame made its own context the current one: the window of the game
        // is made current again, or the game would draw its next frame into this window rather than into its own and stand still.
        _game?.Window.GLContext?.MakeCurrent();
    }

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color) => _renderer?.DrawRectangle(rect, color);

    /// <inheritdoc />
    public void DrawText(string text, Vector2 position, Color color) => _renderer?.DrawText(text, position, color);

    /// <inheritdoc />
    /// <remarks>Text is measured with the built-in font, which is what the host draws its text with, so a label is measured by what draws it.</remarks>
    public Vector2 Measure(string text) => new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);

    /// <inheritdoc />
    /// <remarks>The renderer of this window clips through the device, so a page that is longer than its body is held in by it.</remarks>
    public void PushClip(Rect rect) => _renderer?.PushClip(rect);

    /// <inheritdoc />
    public void PopClip() => _renderer?.PopClip();

    /// <inheritdoc />
    public void Dispose()
    {
        // The objects of this window are deleted on the context that owns them, which is made current here: a window that the window
        // manager asked to close is still there, with its context, until it is disposed, and its renderer has to delete its program,
        // its buffers and its textures while that context is current. A delete that runs on the context of the game instead deletes
        // the object of the game that holds the same number, because two contexts hand the same numbers out.
        IWindow? window = _window;
        window?.GLContext?.MakeCurrent();

        // The callbacks of this window are handed back to the platform with it, so what was installed for a window that is gone does
        // not outlive it: the handle the callback would be called with is one the platform is about to forget.
        if (_glfw is not null && _handle is not null)
        {
            _glfw.SetMouseButtonCallback(_handle, null);
            _glfw.SetCursorEnterCallback(_handle, null);
        }

        _mouseButtonCallback = null;
        _cursorEnterCallback = null;
        _glfw = null;
        _handle = null;

        if (window is not null)
        {
            window.FocusChanged -= OnFocusChanged;
        }

        _focused = false;
        _down = false;

        _renderer?.Dispose();
        _renderer = null;

        window?.Close();
        window?.Dispose();
        _window = null;

        // The window of the game lends a context of its own while this one is open, and the deleting above made the context of this
        // window the current one: the window of the game is made current again, or every draw of the game after this goes to a
        // context that no longer exists and the game stands on a black frame.
        _game?.Window.GLContext?.MakeCurrent();
    }

    /// <summary>Records that the focus of this window arrived or went, which is what tells a game where the keys are going.</summary>
    /// <param name="focused">Whether the window now has the keyboard focus.</param>
    private void OnFocusChanged(bool focused) => _focused = focused;

    /// <summary>Asks the window manager for the focus when the pointer enters the window, which is what sends this window the keys of the keyboard.</summary>
    /// <param name="window">The window the event belongs to.</param>
    /// <param name="entered">Whether the pointer entered the window.</param>
    private void OnCursorEnter(WindowHandle* window, bool entered)
    {
        if (entered)
        {
            _window?.Focus();
        }
    }

    /// <summary>Records that a button of this window went down, which is what a click on a tab or a cross is.</summary>
    /// <param name="window">The window the event belongs to.</param>
    /// <param name="button">The button that changed state.</param>
    /// <param name="action">Whether the button went down, came up or is repeating.</param>
    /// <param name="mods">The modifiers of the keyboard, which this host does not read.</param>
    /// <remarks>
    /// The release of a button is not read here, because the state of the button is asked of the input of the game on every frame:
    /// a release that lands outside the window is a release this window never hears about, and a state that read the events alone
    /// would hold the button down for the rest of the run. What the press carries that the poll of the next frame would not is the
    /// position of the click, which is what a page is handed once.
    /// </remarks>
    private void OnMouseButton(WindowHandle* window, Silk.NET.GLFW.MouseButton button, InputAction action, KeyModifiers mods)
    {
        if (button != Silk.NET.GLFW.MouseButton.Left || action != InputAction.Press)
        {
            return;
        }

        double x = 0;
        double y = 0;
        _glfw?.GetCursorPos(window, out x, out y);
        _clicked = new Vector2((float)x, (float)y);
        _clickReported = true;
    }

    /// <summary>Presents a window of the operating system as the <see cref="IWindowService"/> the renderer attaches to, so the renderer makes its context current and measures the right surface.</summary>
    /// <remarks>
    /// The developer window is a second window and not the one a game plays in: it is never full screen, its vertical sync is off
    /// because it is drawn on the frame the game pumps it rather than on a loop of its own, and it limits no frame rate because
    /// the pace of its frames is the pace of the game. Its title is the one the window was created with, and the icon is the one
    /// the window manager gives it.
    /// </remarks>
    private sealed class HostWindowService(IWindow window) : IWindowService
    {
        public IWindow Window { get; } = window;

        public void Create(int width, int height, string title) => throw new NotSupportedException("This window service presents a window that already exists.");

        public void SetIcon(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException("This window service presents a window that already exists.");

        public void Close() => Window.Close();

        public string Title
        {
            get => Window.Title;
            set => Window.Title = value ?? string.Empty;
        }

        public bool Fullscreen
        {
            get => false;
            set { }
        }

        public bool VSync
        {
            get => false;
            set { }
        }

        public int FpsLimit
        {
            get => 0;
            set { }
        }

        public int RefreshRate => 0;

        public void Apply()
        {
        }
    }
}
