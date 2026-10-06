using System.Diagnostics;
using Age.Core;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Drives the main loop from a Silk.NET window. Blocks the calling thread and returns after the window closes or Stop is called.
/// </summary>
public sealed class SilkGameLoop : IGameLoop
{
    private readonly IWindowService _windowService;
    private volatile bool _stopRequested;

    /// <summary>
    /// Stores the reference only. Does not access IWindowService.Window. The window must be created via Create before Run.
    /// </summary>
    public SilkGameLoop(IWindowService windowService)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        _windowService = windowService;
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

    /// <summary>Intended to be called from the tick callback or the window-close handler. Cross-thread calls are not supported.</summary>
    public void Stop() => _stopRequested = true;
}
