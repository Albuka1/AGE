using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class GameLoopTests
{
    [Fact]
    public void TestGameLoop_RunNSteps_CallsTickNTimes()
    {
        var loop = new TestGameLoop(5, 0.5f);
        var times = new List<GameTime>();

        loop.Run(times.Add);

        times.Should().HaveCount(5);
        times[0].Total.Should().Be(0.5d);
        times[4].Total.Should().Be(2.5d);
    }

    [Fact]
    public void TestGameLoop_RunFixed_AdvancesByTheStep()
    {
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(1d));
        var loop = new TestGameLoop(30, 0.5f, timestep);
        var updates = new List<GameTime>();
        var renders = new List<GameTime>();

        loop.Run(updates.Add, renders.Add);

        updates.Should().HaveCount(60);
        updates[0].Delta.Should().Be(timestep.Step.TotalSeconds);
        updates[59].Total.Should().Be(15d);
        renders.Should().HaveCount(30);
        renders[29].Delta.Should().Be(0.5d);
    }

    [Fact]
    public void TestGameLoop_RunFixed_FramesShorterThanTheStep_UpdateNothing()
    {
        var loop = new TestGameLoop(2, 0.001f, new FixedTimestep(TimeSpan.FromSeconds(0.25d)));
        int updates = 0;
        int renders = 0;

        loop.Run(_ => updates++, _ => renders++);

        updates.Should().Be(0);
        renders.Should().Be(2);
    }

    [Fact]
    public void TestGameLoop_RunFixed_StopsBeforeTheNextFrame()
    {
        var loop = new TestGameLoop(10, 0.5f, new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(1d)));
        int renders = 0;

        loop.Run(
            _ => { },
            _ =>
            {
                renders++;
                if (renders == 3)
                {
                    loop.Stop();
                }
            });

        renders.Should().Be(3);
    }

    [Fact]
    public void TestGameLoop_RunFixed_NullCallbacks_Throw()
    {
        var loop = new TestGameLoop(1, 0.5f);

        FluentActions.Invoking(() => loop.Run(null!, _ => { })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => loop.Run(_ => { }, null!)).Should().Throw<ArgumentNullException>();
    }
}
