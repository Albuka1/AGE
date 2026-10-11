using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;
using Xunit;

namespace Age.Tests;

public sealed class TextInputServiceTests
{
    [Fact]
    public void NullInputService_WithoutTypedCharacters_ReportsNone()
    {
        var input = new NullInputService();

        input.TypedCharacters.Should().BeEmpty();
    }

    [Fact]
    public void NullInputService_TypedCharacters_ReportsWhatTheTestTyped()
    {
        var input = new NullInputService { Typed = "spawn 2" };

        input.TypedCharacters.Should().Be("spawn 2");
    }

    [Fact]
    public void AddAgeInput_ResolvesOneServiceBehindBothInterfaces()
    {
        using ServiceProvider provider = new ServiceCollection().AddAgeInput().BuildServiceProvider();

        IInputService input = provider.GetRequiredService<IInputService>();
        ITextInputService text = provider.GetRequiredService<ITextInputService>();

        text.Should().BeSameAs(input, "a test drives one instance and reads it through both interfaces");
        text.TypedCharacters.Should().BeEmpty();
    }

    [Fact]
    public void AddAgeSilkInput_ResolvesOneServiceBehindBothInterfaces()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeInput()
            .AddAgeSilkInput()
            .AddSingleton<IWindowService, FakeWindowService>()
            .BuildServiceProvider();

        IInputService input = provider.GetRequiredService<IInputService>();
        ITextInputService text = provider.GetRequiredService<ITextInputService>();

        input.Should().BeOfType<SilkInputService>("the service that reads the window answers both interfaces");
        text.Should().BeSameAs(input, "one service reads the window, so the input context is opened once");
    }

    [Fact]
    public void SilkInputService_ReleasedHeld_ForgetsAKeyThatTheFocusChangeSwallowedAReleaseFor()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeInput()
            .AddAgeSilkInput()
            .AddSingleton<IWindowService, FakeWindowService>()
            .BuildServiceProvider();

        var input = (SilkInputService)provider.GetRequiredService<IInputService>();

        // The window lost the focus, which is what opening the developer window does: the key went down through the device and the
        // release never arrives, so the state is dropped rather than left held for the rest of the run.
        input.ReleaseHeld();

        input.IsKeyDown(Key.W).Should().BeFalse("a key the focus change swallowed the release for is not held");
        input.IsKeyPressed(Key.W).Should().BeFalse();
        input.IsMouseButtonDown(MouseButton.Left).Should().BeFalse();
        input.TypedCharacters.Should().BeEmpty();
    }

    /// <summary>The window the test never opens, which is what the Silk.NET input service would read.</summary>
    private sealed class FakeWindowService : IWindowService
    {
        public IWindow Window => throw new NotSupportedException("the test does not open a window");

        public void Create(int width, int height, string title) => throw new NotSupportedException();

        public void SetIcon(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException();

        public void Close() => throw new NotSupportedException();
    }
}
