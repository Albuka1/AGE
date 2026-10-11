using System.Text;
using Age.Core;
using Age.Input;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Reads keyboard and mouse state from a Silk.NET window and answers <see cref="IInputService"/> and
/// <see cref="ITextInputService"/> from it.
/// </summary>
/// <remarks>
/// Silk.NET exposes input through polling, so the device is sampled once per <see cref="BeginFrame"/>, and that is why the
/// frame boundary is part of the input contract. The device events are used as well: a press is recorded the moment the
/// platform reports it, so a key that goes down and up between two samples is still reported as pressed for the frame it
/// happened in. The device is opened on the first frame, so the window has to exist by then. The characters that were
/// typed are collected from the same device, and the frame boundary is what separates the characters of one frame from
/// the next.
/// </remarks>
public sealed class SilkInputService : IInputService, ITextInputService, IClipboardService, IDisposable
{
    private readonly IWindowService _windowService;
    private readonly InputStateTracker _tracker = new();
    private readonly StringBuilder _typed = new();

    private IInputContext? _context;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;

    /// <summary>Initializes the service with the window service it reads from. Nothing is opened until the first frame.</summary>
    /// <param name="windowService">The window service that owns the window to read input from.</param>
    /// <exception cref="ArgumentNullException">The window service is null.</exception>
    public SilkInputService(IWindowService windowService)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        _windowService = windowService;
    }

    /// <summary>Gets the underlying Silk.NET input context. It is created together with the window at the first frame.</summary>
    public IInputContext? Context => _context;

    /// <summary>Forgets every key and button that is being held, which is what a window that loses the focus asks for.</summary>
    /// <remarks>
    /// A device reports a key that went down and one that came up, and a window that loses the focus while a key is held never sees
    /// the release: the key would stay held for the rest of the run, and a game would walk on its own after the person let go. The
    /// state is dropped here instead, together with the characters that were typed and not read, so that a line that was half typed
    /// when the focus moved does not arrive in the console of the frame that follows.
    /// </remarks>
    public void ReleaseHeld()
    {
        _tracker.ReleaseAll();
        _typed.Clear();
    }

    /// <inheritdoc />
    public Vector2 MousePosition => _tracker.MousePosition;

    /// <inheritdoc />
    public float MouseWheel => _tracker.MouseWheel;

    /// <inheritdoc />
    /// <remarks>
    /// The clipboard belongs to the keyboard, which is opened with the window on the first frame, so a read before that answers an
    /// empty text rather than reaching a device that is not there yet.
    /// </remarks>
    public string Text => _keyboard?.ClipboardText ?? string.Empty;

    /// <inheritdoc />
    public void SetText(string? text)
    {
        if (_keyboard is not null)
        {
            _keyboard.ClipboardText = text ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public string TypedCharacters => _typed.ToString();

    /// <inheritdoc />
    public void BeginFrame()
    {
        Attach();

        // The characters of this frame are the ones the keyboard produced since the last one, and they are read from the
        // device as well, so a console sees a character on the frame it arrived in.
        _typed.Clear();
        _tracker.BeginFrame();

        Sample();
    }

    /// <summary>Reports the state of every key and button of the device, which is what a polling backend does once per frame.</summary>
    /// <remarks>
    /// <para>
    /// This is the one place a backend still enumerates the keys, and it has to: a backend answers a question about one key at a
    /// time, so the frame is where the whole keyboard is asked about. The engine keeps no list of its own, so a game asks about any
    /// key and the frame reports every one of them.
    /// </para>
    /// <para>
    /// A value that this window has no key for is skipped rather than asked about: the device refuses a name it does not carry,
    /// and that is not a mistake of a game, which simply never asks about such a key. A press that the platform sent as an event
    /// reached the tracker before this, so a key that went down and up inside one frame is still reported as pressed for it.
    /// </para>
    /// </remarks>
    private void Sample()
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            try
            {
                _tracker.SetKey(key, _keyboard!.IsKeyPressed(key));
            }
            catch (Exception exception) when (exception is ArgumentException or GlfwException)
            {
                // The key is not one this window knows, so nothing of a game can be holding it.
            }
        }

        foreach (MouseButton button in Enum.GetValues<MouseButton>())
        {
            try
            {
                _tracker.SetMouseButton(button, _mouse!.IsButtonPressed(button));
            }
            catch (Exception exception) when (exception is ArgumentException or GlfwException)
            {
                // The button is not one this window knows.
            }
        }

        _tracker.MouseMove(new Vector2(_mouse!.Position.X, _mouse.Position.Y));
    }

    /// <inheritdoc />
    public bool IsKeyDown(Key key) => _tracker.IsKeyDown(key);

    /// <inheritdoc />
    public bool IsKeyPressed(Key key) => _tracker.IsKeyPressed(key);

    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => _tracker.IsMouseButtonDown(button);

    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => _tracker.IsMouseButtonPressed(button);

    /// <summary>Records how far the wheel of the mouse was turned, which the device reports as an event rather than as a state.</summary>
    private void OnMouseWheel(IMouse mouse, ScrollWheel wheel) => _tracker.SetMouseWheel(wheel.Y);

    /// <summary>Forgets what is held when the window of the game takes or loses the keyboard focus.</summary>
    /// <param name="focused">Whether the window now has the keyboard focus.</param>
    /// <remarks>
    /// A device reports a key that went down and one that came up, and a window that loses the focus while a key is held never sees
    /// the release. What is held is forgotten on the way out and on the way in alike, so a key that was down when a window beside the
    /// game took the keyboard is not a key that is still down when the game gets it back.
    /// </remarks>
    private void OnFocusChanged(bool focused) => ReleaseHeld();

    /// <summary>Closes the input context and releases its devices. Calling it more than once does nothing.</summary>
    public void Dispose()
    {
        if (_context is not null)
        {
            _windowService.Window.FocusChanged -= OnFocusChanged;
        }

        _context?.Dispose();
        _context = null;
        _keyboard = null;
        _mouse = null;
    }

    private void Attach()
    {
        if (_context is not null)
        {
            return;
        }

        IWindow window = _windowService.Window;

        // A window that loses the focus never sees the release of a key that was held while it had it, so what is held is forgotten:
        // a person who was walking when they clicked the developer window does not come back to a game that walks on its own.
        window.FocusChanged += OnFocusChanged;

        _context = window.CreateInput();
        _keyboard = _context.Keyboards.Count > 0
            ? _context.Keyboards[0]
            : throw new InvalidOperationException("The window has no keyboard.");
        _mouse = _context.Mice.Count > 0
            ? _context.Mice[0]
            : throw new InvalidOperationException("The window has no mouse.");

        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
        _keyboard.KeyChar += OnKeyChar;
        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.Scroll += OnMouseWheel;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode) => _tracker.KeyDown(key);

    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode) => _tracker.KeyUp(key);

    /// <summary>Records a character that the keyboard produced, which is what a console or a text field reads.</summary>
    private void OnKeyChar(IKeyboard keyboard, char character) => _typed.Append(character);

    private void OnMouseDown(IMouse mouse, MouseButton button) => _tracker.MouseDown(button);

    private void OnMouseUp(IMouse mouse, MouseButton button) => _tracker.MouseUp(button);
}
