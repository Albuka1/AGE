using Age.Core;
using Age.Input;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class InputStateTrackerTests
{
    [Fact]
    public void InputStateTracker_KeyDown_IsDownAndPressed()
    {
        var tracker = new InputStateTracker();

        tracker.KeyDown(Key.W);

        tracker.IsKeyDown(Key.W).Should().BeTrue();
        tracker.IsKeyPressed(Key.W).Should().BeTrue();
    }

    [Fact]
    public void InputStateTracker_NextFrame_KeepsDownAndClearsPressed()
    {
        var tracker = new InputStateTracker();
        tracker.KeyDown(Key.W);

        tracker.BeginFrame();

        tracker.IsKeyDown(Key.W).Should().BeTrue();
        tracker.IsKeyPressed(Key.W).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_RepeatedKeyDown_ReportsPressedOncePerFrame()
    {
        var tracker = new InputStateTracker();

        tracker.KeyDown(Key.W);
        tracker.KeyDown(Key.W);

        tracker.IsKeyPressed(Key.W).Should().BeTrue();
    }

    [Fact]
    public void InputStateTracker_KeyUp_ClearsDown()
    {
        var tracker = new InputStateTracker();
        tracker.KeyDown(Key.W);

        tracker.KeyUp(Key.W);

        tracker.IsKeyDown(Key.W).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_KeyReleasedInTheSameFrame_StillReportsPressed()
    {
        var tracker = new InputStateTracker();

        tracker.KeyDown(Key.Space);
        tracker.KeyUp(Key.Space);

        tracker.IsKeyDown(Key.Space).Should().BeFalse();
        tracker.IsKeyPressed(Key.Space).Should().BeTrue();
    }

    [Fact]
    public void InputStateTracker_MousePosition_ReportsLastMove()
    {
        var tracker = new InputStateTracker();

        tracker.MouseMove(new Vector2(12f, 34f));

        tracker.MousePosition.Should().Be(new Vector2(12f, 34f));
    }

    [Fact]
    public void InputStateTracker_MouseButtons_TrackEachButton()
    {
        var tracker = new InputStateTracker();

        tracker.MouseDown(MouseButton.Left);
        tracker.MouseDown(MouseButton.Right);
        tracker.BeginFrame();

        tracker.IsMouseButtonDown(MouseButton.Left).Should().BeTrue();
        tracker.IsMouseButtonPressed(MouseButton.Left).Should().BeFalse();
        tracker.IsMouseButtonDown(MouseButton.Middle).Should().BeFalse();
        tracker.IsMouseButtonPressed(MouseButton.Right).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_SetKey_ReportsPressedOnTheFirstFrameOnly()
    {
        var tracker = new InputStateTracker();

        tracker.SetKey(Key.W, true);
        bool pressedOnTheFrameItWentDown = tracker.IsKeyPressed(Key.W);
        tracker.BeginFrame();
        tracker.SetKey(Key.W, true);

        pressedOnTheFrameItWentDown.Should().BeTrue();
        tracker.IsKeyDown(Key.W).Should().BeTrue();
        tracker.IsKeyPressed(Key.W).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_SetMouseButton_ReportsPressedOnTheFirstFrameOnly()
    {
        var tracker = new InputStateTracker();

        tracker.SetMouseButton(MouseButton.Left, true);
        bool pressedOnTheFrameItWentDown = tracker.IsMouseButtonPressed(MouseButton.Left);
        tracker.BeginFrame();
        tracker.SetMouseButton(MouseButton.Left, true);

        pressedOnTheFrameItWentDown.Should().BeTrue();
        tracker.IsMouseButtonDown(MouseButton.Left).Should().BeTrue();
        tracker.IsMouseButtonPressed(MouseButton.Left).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_SetKeyToUp_ClearsDown()
    {
        var tracker = new InputStateTracker();
        tracker.SetKey(Key.W, true);

        tracker.SetKey(Key.W, false);

        tracker.IsKeyDown(Key.W).Should().BeFalse();
    }

    [Fact]
    public void InputStateTracker_ReleaseAll_DropsKeysAndButtonsButKeepsPosition()
    {
        var tracker = new InputStateTracker();
        tracker.KeyDown(Key.W);
        tracker.MouseDown(MouseButton.Left);
        tracker.MouseMove(new Vector2(5f, 6f));

        tracker.ReleaseAll();

        tracker.IsKeyDown(Key.W).Should().BeFalse();
        tracker.IsKeyPressed(Key.W).Should().BeFalse();
        tracker.IsMouseButtonDown(MouseButton.Left).Should().BeFalse();
        tracker.MousePosition.Should().Be(new Vector2(5f, 6f));
    }
}
