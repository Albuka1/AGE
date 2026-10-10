using Age.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class ConsoleLoggerProviderTests
{
    [Fact]
    public void ConsoleLoggerProvider_Log_WritesALineThatNamesTheLevelAndTheCategory()
    {
        var console = new ConsoleService();
        ILogger logger = new ConsoleLoggerProvider(console).CreateLogger("Age.Assets");

        logger.LogInformation("Loaded {Path}.", "tiles.bmp");

        console.Output.Should().ContainSingle().Which.Should().Be("[Information] Age.Assets: Loaded tiles.bmp.");
    }

    [Fact]
    public void ConsoleLoggerProvider_LogAtErrorAndAbove_WritesAnErrorLineThatHoldsTheException()
    {
        var console = new ConsoleService();
        ILogger logger = new ConsoleLoggerProvider(console).CreateLogger("Age.Rendering");

        logger.LogError(new InvalidOperationException("the context is gone"), "The frame was not drawn.");

        console.Output.Should().ContainSingle();
        console.Output[0].Should().Be("error: [Error] Age.Rendering: The frame was not drawn. (InvalidOperationException: the context is gone)");
    }

    [Fact]
    public void ConsoleLoggerProvider_LogAMessageThatAlreadyHoldsTheException_DoesNotRepeatIt()
    {
        var console = new ConsoleService();
        ILogger logger = new ConsoleLoggerProvider(console).CreateLogger("Age.Core");
        var exception = new InvalidDataException("The scene holds no entities.");

        logger.LogError(exception, "The scene holds no entities.");

        console.Output.Should().ContainSingle().Which.Should().Be("error: [Error] Age.Core: The scene holds no entities.");
    }

    [Fact]
    public void ConsoleLoggerProvider_LogAtWarning_WritesAWarningLine()
    {
        var console = new ConsoleService();
        ILogger logger = new ConsoleLoggerProvider(console).CreateLogger("Age.Assets");

        logger.LogWarning("The image {Path} is not there.", "tiles.bmp");

        console.Lines.Should().ContainSingle().Which.Level.Should().Be(ConsoleLevel.Warning);
        console.Lines[0].Text.Should().Be("[Warning] Age.Assets: The image tiles.bmp is not there.");
    }

    [Fact]
    public void ConsoleLoggerProvider_IsEnabled_LeavesTheLevelsToTheLoggingBuilder()
    {
        ILogger logger = new ConsoleLoggerProvider(new ConsoleService()).CreateLogger("Age.Core");

        logger.IsEnabled(LogLevel.Trace).Should().BeTrue("a record that reaches the provider is one the builder already let through");
        logger.IsEnabled(LogLevel.None).Should().BeFalse();
        logger.BeginScope("a scope").Should().BeNull("the console holds no scopes");
    }
}
