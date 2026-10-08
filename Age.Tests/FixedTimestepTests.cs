using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class FixedTimestepTests
{
    [Fact]
    public void FixedTimestep_FrameEqualToStep_ReportsOneStep()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.5d), TimeSpan.FromSeconds(2d));
        var steps = new List<GameTime>();

        int count = timestep.Advance(Frame(0.5d), steps.Add);

        count.Should().Be(1);
        steps.Should().ContainSingle();
        steps[0].Delta.Should().BeApproximately(0.5d, 1e-9d);
        steps[0].Total.Should().BeApproximately(0.5d, 1e-9d);
        timestep.Alpha.Should().Be(0d);
        timestep.Elapsed.Should().BeApproximately(0.5d, 1e-9d);
    }

    [Fact]
    public void FixedTimestep_LongFrame_ReportsSeveralSteps()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(4d));

        int count = timestep.Advance(Frame(1d), _ => { });

        count.Should().Be(4);
        timestep.Elapsed.Should().BeApproximately(1d, 1e-9d);
        timestep.Alpha.Should().Be(0d);
    }

    [Fact]
    public void FixedTimestep_ShortFrames_AccumulateIntoOneStep()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d));
        int steps = 0;

        timestep.Advance(Frame(0.04d), _ => steps++);
        timestep.Advance(Frame(0.04d), _ => steps++);

        steps.Should().Be(0);

        timestep.Advance(Frame(0.04d), _ => steps++);

        steps.Should().Be(1);
        timestep.Alpha.Should().BeGreaterThan(0d).And.BeLessThan(1d);
    }

    [Fact]
    public void FixedTimestep_FrameLongerThanTheMaximum_IsClamped()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(0.25d));

        int count = timestep.Advance(Frame(10d), _ => { });

        count.Should().Be(2);
        timestep.Elapsed.Should().BeApproximately(0.2d, 1e-9d);
    }

    [Fact]
    public void FixedTimestep_NegativeOrNaNFrame_ReportsNothing()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d));

        timestep.Advance(Frame(-5d), _ => { }).Should().Be(0);
        timestep.Advance(Frame(double.NaN), _ => { }).Should().Be(0);
        timestep.Alpha.Should().Be(0d);
    }

    [Fact]
    public void FixedTimestep_StepsCarryARunningTotal()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(1d));
        var totals = new List<double>();

        timestep.Advance(Frame(0.25d), time => totals.Add(time.Total));
        timestep.Advance(Frame(0.5d), time => totals.Add(time.Total));

        totals.Should().HaveCount(3);
        totals[0].Should().BeApproximately(0.25d, 1e-9d);
        totals[1].Should().BeApproximately(0.5d, 1e-9d);
        totals[2].Should().BeApproximately(0.75d, 1e-9d);
    }

    [Fact]
    public void FixedTimestep_ZeroStep_Throws() =>
        FluentActions.Invoking(() => new FixedTimestep(TimeSpan.Zero)).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void FixedTimestep_MaximumShorterThanTheStep_Throws() =>
        FluentActions.Invoking(() => new FixedTimestep(TimeSpan.FromSeconds(0.2d), TimeSpan.FromSeconds(0.1d)))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void FixedTimestep_NullUpdate_Throws() =>
        FluentActions.Invoking(() => new FixedTimestep(TimeSpan.FromSeconds(0.1d)).Advance(Frame(1d), null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void FixedTimestep_PausedClock_ReportsNothingAndDiscardsThePendingTime()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(2d)) { Paused = true };
        int steps = 0;

        timestep.Advance(Frame(1d), _ => steps++);
        timestep.Advance(Frame(1d), _ => steps++);

        steps.Should().Be(0, "a paused clock ignores the time of the frames that pass");
        timestep.Elapsed.Should().Be(0d);
        timestep.Tick.Should().Be(0);
        timestep.Alpha.Should().Be(0d, "the time of a paused frame is discarded rather than accumulated");

        timestep.Paused = false;
        timestep.Advance(Frame(0.05d), _ => steps++);

        steps.Should().Be(0, "a game that was paused for a while does not resume by running the steps it missed");

        timestep.Advance(Frame(0.05d), _ => steps++);

        steps.Should().Be(1);
    }

    [Fact]
    public void FixedTimestep_PausedFromInsideAStep_StopsTheFrameAndDropsWhatIsLeftOfIt()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(2d));
        var steps = new List<GameTime>();

        int reported = timestep.Advance(Frame(0.4d), time =>
        {
            steps.Add(time);

            // What a game does when the player presses pause while a step runs: the frame still covers three more steps.
            timestep.Paused = true;
        });

        reported.Should().Be(1, "the clock stops in the step the game paused in, not at the end of the frame");
        steps.Should().ContainSingle();
        timestep.Tick.Should().Be(1);
        timestep.Alpha.Should().Be(0d, "what was left of the frame is dropped rather than turned into steps later");

        timestep.Paused = false;
        timestep.Advance(Frame(0.05d), _ => { });

        timestep.Tick.Should().Be(1, "the dropped time is not replayed after the game resumes");
    }

    [Fact]
    public void FixedTimestep_ZeroTimeScaleAndEndlessFrame_DoesNotPoisonTheClock()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(0.25d)) { TimeScale = 0d };

        timestep.Advance(Frame(double.PositiveInfinity), _ => { }).Should().Be(0);
        timestep.Alpha.Should().Be(0d, "the scaled delta is not a number, so it counts as no time at all");

        timestep.TimeScale = 1d;

        timestep.Advance(Frame(0.1d), _ => { }).Should().Be(1, "the clock still runs after a frame that scaled to NaN");
    }

    [Fact]
    public void FixedTimestep_EndlessFrame_IsClampedToTheMaximumFrameTime()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(0.25d));

        timestep.Advance(Frame(double.PositiveInfinity), _ => { }).Should().Be(2, "an endless frame counts as the longest frame that is taken into account");
        timestep.Alpha.Should().BeApproximately(0.5d, 1e-9d);
    }

    [Fact]
    public void FixedTimestep_TimeScale_StretchesAndCompressesTheTimeOfTheFrames()
    {
        var slow = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(2d)) { TimeScale = 0.5d };
        var fast = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(2d)) { TimeScale = 2d };
        int slowSteps = 0;
        int fastSteps = 0;

        slow.Advance(Frame(0.1d), _ => slowSteps++);
        slow.Advance(Frame(0.1d), _ => slowSteps++);
        fast.Advance(Frame(0.1d), _ => fastSteps++);

        slowSteps.Should().Be(1, "half of the time of the frames reached a whole step");
        fastSteps.Should().Be(2, "twice the time of one frame covers two steps");
        slow.Elapsed.Should().BeApproximately(0.1d, 1e-9d);
        fast.Elapsed.Should().BeApproximately(0.2d, 1e-9d);
    }

    [Fact]
    public void FixedTimestep_Tick_CountsTheStepsThatWereReported()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.1d), TimeSpan.FromSeconds(1d));

        timestep.Tick.Should().Be(0);

        timestep.Advance(Frame(0.35d), _ => { });

        timestep.Tick.Should().Be(3, "three whole steps fit in a frame of 0.35 s");
        timestep.Elapsed.Should().BeApproximately(0.3d, 1e-9d);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FixedTimestep_TimeScaleThatIsNotUsable_Throws(double scale) =>
        FluentActions.Invoking(() => new FixedTimestep(TimeSpan.FromSeconds(0.1d)).TimeScale = scale)
            .Should().Throw<ArgumentOutOfRangeException>();

    private static GameTime Frame(double delta) => new(delta, 0d);
}
