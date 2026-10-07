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
    private volatile bool _stopRequested;

    /// <summary>
    /// Stores the references only. Does not access IWindowService.Window. The window must be created via Create before Run.
    /// </summary>
    /// <param name="windowService">The window that provides the frame timing.</param>
    /// <param name="inputServices">The input services to open each frame with. The sequence is empty when no input is registered.</param>
    public SilkGameLoop(IWindowService windowService, IEnumerable<IInputService> inputServices)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(inputServices);
        _windowService = windowService;
        _inputServices = [.. inputServices];
    }

    /// <inheritdoc />
    public void Run(Action<GameTime> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

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
