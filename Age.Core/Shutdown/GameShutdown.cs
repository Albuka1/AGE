using System.Runtime.ExceptionServices;

namespace Age.Core;

/// <summary>
/// Releases what a game holds, in one call, in the order that the registered <see cref="IGameShutdownStep"/> values ask
/// for.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Run"/> while the window is still open and before the container is disposed: every engine object that
/// lives in a device context is released there, and the services that the container disposes afterwards find nothing
/// left to do.
/// </para>
/// <para>
/// A step that throws does not stop the steps behind it, because a shutdown that stopped halfway would leak the device
/// objects of everything that was skipped. The first failure is thrown after every step ran, so a game learns about it
/// and still leaves a clean frame behind. The call itself is idempotent.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// provider.GetRequiredService&lt;GameShutdown&gt;().Run();
/// </code>
/// </example>
public sealed class GameShutdown
{
    private readonly IGameShutdownStep[] _steps;
    private bool _ran;

    /// <summary>Initializes the shutdown from the steps that the assemblies of the engine and the game registered.</summary>
    /// <param name="steps">The steps, which run from the lowest <see cref="IGameShutdownStep.Order"/> to the highest.</param>
    /// <exception cref="ArgumentNullException">The steps are null.</exception>
    public GameShutdown(IEnumerable<IGameShutdownStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = [.. steps.OrderBy(step => step.Order)];
    }

    /// <summary>Gets the number of steps, which tells a game whether there is anything to release.</summary>
    public int Count => _steps.Length;

    /// <summary>Runs every step once, in the order of their <see cref="IGameShutdownStep.Order"/>, and runs the remaining ones even when one fails.</summary>
    /// <exception cref="Exception">A step failed. Every step ran before this call threw.</exception>
    public void Run()
    {
        if (_ran)
        {
            return;
        }

        _ran = true;
        ExceptionDispatchInfo? failure = null;

        foreach (IGameShutdownStep step in _steps)
        {
            try
            {
                step.Shutdown();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        failure?.Throw();
    }
}
