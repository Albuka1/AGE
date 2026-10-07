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
        steps[0].Delta.Should().Be(0.5d);
        steps[0].Total.Should().Be(0.5d);
        timestep.Alpha.Should().Be(0d);
        timestep.Elapsed.Should().Be(0.5d);
    }

    [Fact]
    public void FixedTimestep_LongFrame_ReportsSeveralSteps()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(4d));

        int count = timestep.Advance(Frame(1d), _ => { });

        count.Should().Be(4);
        timestep.Elapsed.Should().Be(1d);
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
        timestep.Elapsed.Should().Be(0.2d);
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

        totals.Should().Equal(0.25d, 0.5d, 0.75d);
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

    private static GameTime Frame(double delta) => new(delta, 0d);
}
