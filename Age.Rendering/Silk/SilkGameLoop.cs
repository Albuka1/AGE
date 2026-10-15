using System.Diagnostics;
using Age.Core;
using Age.Input;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Drives the main loop from a Silk.NET window. Blocks the calling thread and returns after the window closes or Stop is called.
/// </summary>
/// <remarks>
/// The loop limits the frame rate itself, which is what lets a game cap the pace of its frames without turning on the vertical sync
/// of the device: the limit is read from <see cref="IWindowService.FpsLimit"/> on every frame, and it falls back to the refresh
/// rate of the monitor when vertical sync is on and no limit was asked for, because the device already caps the frame rate at
/// the monitor in that case and the loop adds nothing to it.
/// </remarks>
public sealed class SilkGameLoop : IGameLoop
{
    /// <summary>The pace of the frame that the loop keeps when it limits the frame rate, in seconds.</summary>
    private readonly Stopwatch _frameWatch = Stopwatch.StartNew();

    private readonly IWindowService _windowService;
    private readonly IInputService[] _inputServices;
    private readonly FixedTimestep _timestep;
    private volatile bool _stopRequested;

    /// <summary>
    /// Stores the references only. Does not access IWindowService.Window. The window must be created via Create before Run.
    /// </summary>
    /// <param name="windowService">The window that provides the frame timing and the frame rate limit.</param>
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
            long frameStart = _frameWatch.ElapsedTicks;

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

            LimitFrame(frameStart);
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

    /// <summary>Holds the pace of the frame to the limit the window asks for, and returns at once when it asks for none.</summary>
    /// <param name="frameStart">The tick of <see cref="_frameWatch"/> at which the frame began.</param>
    /// <remarks>
    /// The limit is read from the window on every frame rather than remembered, so a game that changes it while it runs is obeyed on
    /// the frame that follows. A limit of zero means the loop does not limit at all, unless vertical sync is on, in which case the
    /// refresh rate of the monitor is used: the device waits for the vertical blank either way, and the loop only keeps the frame
    /// from running ahead of the monitor between two blanks.
    /// </remarks>
    private void LimitFrame(long frameStart)
    {
        int fps = _windowService.FpsLimit;

        if (fps <= 0 && _windowService.VSync)
        {
            fps = _windowService.RefreshRate;
        }

        if (fps <= 0)
        {
            return;
        }

        // The pace is measured in ticks of the stopwatch rather than in milliseconds, because a millisecond is a long time at the
        // rates a display runs at: a frame of 144 hertz is 6.94 milliseconds, and rounding that to whole milliseconds adds up to a
        // visible jitter. The sleep is asked for one millisecond short of the pace and the rest is spent in a spin, because the
        // operating system wakes a sleeping thread late by a fraction of a millisecond and never early.
        long target = Stopwatch.Frequency / fps;
        long spent = _frameWatch.ElapsedTicks - frameStart;
        long remaining = target - spent;

        if (remaining <= 0)
        {
            return;
        }

        double milliseconds = (remaining * 1000.0) / Stopwatch.Frequency;

        if (milliseconds > 1.0)
        {
            Thread.Sleep((int)(milliseconds - 1.0));
        }

        while (_frameWatch.ElapsedTicks - frameStart < target)
        {
            Thread.SpinWait(32);
        }
    }

    /// <summary>Intended to be called from the tick callback or the window-close handler. Cross-thread calls are not supported.</summary>
    public void Stop() => _stopRequested = true;
}
