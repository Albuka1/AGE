using System.Diagnostics;
using Age.Core;
using Age.Input;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Drives the main loop from a Silk.NET window. Blocks the calling thread and returns after the window closes or Stop is called.
/// </summary>
public sealed class SilkGameLoop : IGameLoop
{
    private readonly IWindowService _windowService;
    private readonly IInputService[] _inputServices;
    private readonly FixedTimestep _timestep;
    private volatile bool _stopRequested;

    /// <summary>
    /// Stores the references only. Does not access IWindowService.Window. The window must be created via Create before Run.
    /// </summary>
    /// <param name="windowService">The window that provides the frame timing.</param>
    /// <param name="inputServices">The input services to open each frame with. The sequence is empty when no input is registered.</param>
    /// <param name="timestep">The fixed step that the two-callback overload advances with. Register your own before resolving the loop to change it.</param>
    public SilkGameLoop(IWindowService windowService, IEnumerable<IInputService> inputServices, FixedTimestep timestep)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(inputServices);
        ArgumentNullException.ThrowIfNull(timestep);
        _windowService = windowService;
        _inputServices = [.. inputServices];
        _timestep = timestep;
    }

    /// <inheritdoc />
    public void Run(Action<GameTime> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        RunFrames(tick);
    }

    /// <inheritdoc />
    public void Run(Action<GameTime> update, Action<GameTime> render)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(render);

        RunFrames(time =>
        {
            _timestep.Advance(time, update);
            render(time);
        });
    }

    /// <summary>Runs the window pump until the window closes or Stop is called, passing the time of each frame to the callback.</summary>
    private void RunFrames(Action<GameTime> tick)
    {
        IWindow window = _windowService.Window;
        window.Closing += () => _stopRequested = true;
        window.GLContext?.MakeCurrent();

        var clock = Stopwatch.StartNew();
        double previous = 0d;

        while (!_stopRequested && !window.IsClosing)
        {
            OpenInputFrame();

            window.DoEvents();

            if (_stopRequested || window.IsClosing)
            {
                break;
            }

            double elapsed = clock.Elapsed.TotalSeconds;
            tick(new GameTime(elapsed - previous, elapsed));
            previous = elapsed;

            // The context of the game window is made current again before the buffers are swapped, because a frame of the game may
            // have drawn on a window of its own — a developer window is one — and left the context of that window current: the
            // swap belongs to the window of the game, and what it shows is drawn through the context that is current here.
            window.GLContext?.MakeCurrent();
            window.SwapBuffers();
        }
    }

    /// <summary>Opens the input frame of every registered input service, so the systems see the state of this frame, including the events that the window pump delivers into it.</summary>
    private void OpenInputFrame()
    {
        foreach (IInputService input in _inputServices)
        {
            input.BeginFrame();
        }
    }

    /// <summary>Intended to be called from the tick callback or the window-close handler. Cross-thread calls are not supported.</summary>
    public void Stop() => _stopRequested = true;
}
