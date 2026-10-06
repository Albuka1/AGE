using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Owns the application window.
/// </summary>
public interface IWindowService
{
    /// <summary>Gets the underlying window.</summary>
    IWindow Window { get; }

    /// <summary>Creates the window. It must be called before the game loop runs.</summary>
    void Create(int width, int height, string title);

    /// <summary>Closes the window.</summary>
    void Close();
}
