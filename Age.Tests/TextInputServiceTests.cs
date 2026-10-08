using Age.Input;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
}
