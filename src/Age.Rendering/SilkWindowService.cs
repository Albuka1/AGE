using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Creates and owns a Silk.NET window with an OpenGL 3.3 core profile context.
/// </summary>
public sealed class SilkWindowService : IWindowService
{
    private IWindow? _window;

    /// <inheritdoc />
    public IWindow Window => _window ?? throw new InvalidOperationException("The window has not been created. Call Create first.");

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
        };

        _window = Silk.NET.Windowing.Window.Create(options);
        _window.Initialize();
    }

    /// <inheritdoc />
    public void Close() => _window?.Close();
}
