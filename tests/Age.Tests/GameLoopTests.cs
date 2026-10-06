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
}
