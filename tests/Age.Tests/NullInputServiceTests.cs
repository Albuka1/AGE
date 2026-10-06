using Age.Core;
using Age.Input;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class NullInputServiceTests
{
    [Fact]
    public void NullInputService_LeftButtonHeld_ReportsPressedOnTheFirstFrameOnly()
    {
        var input = new NullInputService { State = new UIInputState(new Vector2(10f, 20f), true) };

        input.BeginFrame();
        bool pressedOnTheFirstFrame = input.IsMouseButtonPressed(MouseButton.Left);
        input.BeginFrame();

        pressedOnTheFirstFrame.Should().BeTrue();
        input.IsMouseButtonDown(MouseButton.Left).Should().BeTrue();
        input.IsMouseButtonPressed(MouseButton.Left).Should().BeFalse();
    }

    [Fact]
    public void NullInputService_ReleasedAndPressedAgain_ReportsPressedAgain()
    {
        var input = new NullInputService { State = new UIInputState(Vector2.Zero, true) };
        input.BeginFrame();

        input.State = new UIInputState(Vector2.Zero, false);
        input.BeginFrame();
        input.State = new UIInputState(Vector2.Zero, true);
        input.BeginFrame();

        input.IsMouseButtonPressed(MouseButton.Left).Should().BeTrue();
    }

    [Fact]
    public void NullInputService_OtherButtons_AreNeverDown()
    {
        var input = new NullInputService { State = new UIInputState(Vector2.Zero, true) };
        input.BeginFrame();

        input.IsMouseButtonDown(MouseButton.Right).Should().BeFalse();
        input.IsMouseButtonDown(MouseButton.Middle).Should().BeFalse();
        input.IsMouseButtonPressed(MouseButton.Right).Should().BeFalse();
    }
}
