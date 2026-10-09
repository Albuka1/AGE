using System.Text;
using Age.Core;
using Age.Input;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Key = Age.Input.Key;
using MouseButton = Age.Input.MouseButton;
using SilkKey = Silk.NET.Input.Key;
using SilkMouseButton = Silk.NET.Input.MouseButton;

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
public sealed class SilkInputService : IInputService, ITextInputService, IDisposable
{
    private static readonly (SilkKey Silk, Key Engine)[] KeyMap =
    [
        (SilkKey.W, Key.W),
        (SilkKey.A, Key.A),
        (SilkKey.S, Key.S),
        (SilkKey.D, Key.D),
        (SilkKey.Q, Key.Q),
        (SilkKey.E, Key.E),
        (SilkKey.R, Key.R),
        (SilkKey.F, Key.F),
        (SilkKey.G, Key.G),
        (SilkKey.Up, Key.Up),
        (SilkKey.Down, Key.Down),
        (SilkKey.Left, Key.Left),
        (SilkKey.Right, Key.Right),
        (SilkKey.Space, Key.Space),
        (SilkKey.ShiftLeft, Key.Shift),
        (SilkKey.ShiftRight, Key.Shift),
        (SilkKey.ControlLeft, Key.Ctrl),
        (SilkKey.ControlRight, Key.Ctrl),
        (SilkKey.AltLeft, Key.Alt),
        (SilkKey.AltRight, Key.Alt),
        (SilkKey.Enter, Key.Enter),
        (SilkKey.Escape, Key.Escape),
        (SilkKey.Tab, Key.Tab),
        (SilkKey.Backspace, Key.Backspace),
        (SilkKey.F1, Key.F1),
        (SilkKey.Number0, Key.Digit0),
        (SilkKey.Number1, Key.Digit1),
        (SilkKey.Number2, Key.Digit2),
        (SilkKey.Number3, Key.Digit3),
        (SilkKey.Number4, Key.Digit4),
        (SilkKey.Number5, Key.Digit5),
        (SilkKey.Number6, Key.Digit6),
        (SilkKey.Number7, Key.Digit7),
        (SilkKey.Number8, Key.Digit8),
        (SilkKey.Number9, Key.Digit9),
    ];

    private static readonly (SilkMouseButton Silk, MouseButton Engine)[] MouseMap =
    [
        (SilkMouseButton.Left, MouseButton.Left),
        (SilkMouseButton.Right, MouseButton.Right),
        (SilkMouseButton.Middle, MouseButton.Middle),
    ];

    private static readonly Key[] EngineKeys = [.. KeyMap.Select(entry => entry.Engine).Distinct()];

    private static readonly HashSet<Key> GroupedKeys =
        [.. KeyMap.GroupBy(entry => entry.Engine).Where(group => group.Count() > 1).Select(group => group.Key)];

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

    /// <inheritdoc />
    public Vector2 MousePosition => _tracker.MousePosition;

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

        foreach (Key engine in EngineKeys)
        {
            _tracker.SetKey(engine, IsKeyboardKeyDown(engine));
        }

        foreach ((SilkMouseButton silk, MouseButton engine) in MouseMap)
        {
            _tracker.SetMouseButton(engine, _mouse!.IsButtonPressed(silk));
        }

        _tracker.MouseMove(new Vector2(_mouse!.Position.X, _mouse.Position.Y));
    }

    /// <summary>
    /// Returns whether any physical key that maps to the engine key is down. Both Shift keys map to <see cref="Key.Shift"/>,
    /// and Control and Alt work the same way, so their states have to be combined: sampling them one after another would
    /// let the released right Shift clear the held left one.
    /// </summary>
    private bool IsKeyboardKeyDown(Key engine)
    {
        foreach ((SilkKey silk, Key mapped) in KeyMap)
        {
            if (mapped == engine && _keyboard!.IsKeyPressed(silk))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsKeyDown(Key key) => _tracker.IsKeyDown(key);

    /// <inheritdoc />
    public bool IsKeyPressed(Key key) => _tracker.IsKeyPressed(key);

    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => _tracker.IsMouseButtonDown(button);

    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => _tracker.IsMouseButtonPressed(button);

    /// <summary>Closes the input context and releases its devices. Calling it more than once does nothing.</summary>
    public void Dispose()
    {
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
    }

    private void OnKeyDown(IKeyboard keyboard, SilkKey key, int scancode) => RecordKey(key, isDown: true);

    private void OnKeyUp(IKeyboard keyboard, SilkKey key, int scancode) => RecordKey(key, isDown: false);

    /// <summary>Records a character that the keyboard produced, which is what a console or a text field reads.</summary>
    private void OnKeyChar(IKeyboard keyboard, char character) => _typed.Append(character);

    private void OnMouseDown(IMouse mouse, SilkMouseButton button) => RecordButton(button, isDown: true);

    private void OnMouseUp(IMouse mouse, SilkMouseButton button) => RecordButton(button, isDown: false);

    private void RecordKey(SilkKey key, bool isDown)
    {
        foreach ((SilkKey silk, Key engine) in KeyMap)
        {
            if (silk != key)
            {
                continue;
            }

            if (GroupedKeys.Contains(engine))
            {
                // Both physical keys of a modifier map to one engine key, so the state is read back from the device,
                // which already reflects this event: otherwise releasing the right Shift would clear a held left one.
                _tracker.SetKey(engine, IsKeyboardKeyDown(engine));
                return;
            }

            _tracker.SetKey(engine, isDown);
            return;
        }
    }

    private void RecordButton(SilkMouseButton button, bool isDown)
    {
        foreach ((SilkMouseButton silk, MouseButton engine) in MouseMap)
        {
            if (silk == button)
            {
                _tracker.SetMouseButton(engine, isDown);
                return;
            }
        }
    }
}
