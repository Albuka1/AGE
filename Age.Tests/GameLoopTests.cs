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
        updates[59].Total.Should().BeApproximately(15d, 1e-9d);
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
    public void TestGameLoop_RunFixed_PausedClock_UpdatesNothingAndStillRunsTheFrames()
    {
        var world = new World();
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(1d)) { Paused = true };
        var loop = new TestGameLoop(4, 0.5f, timestep);
        var steps = new List<int>();
        var frames = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new CountingSystem(steps));
        pipeline.AddFrame(new CountingFrameSystem(frames));

        loop.Run(
            update: step => world.Update(step, pipeline),
            render: frame => world.UpdateFrame(frame, pipeline));

        steps.Should().BeEmpty("a paused clock reports no step, so the simulation stands still");
        frames.Should().HaveCount(4, "the frames of a paused game still run, which is what its interface needs");
        timestep.Tick.Should().Be(0);
    }

    [Fact]
    public void TestGameLoop_RunFixed_PausingFromInsideAStep_StopsTheSimulationAndKeepsTheFrames()
    {
        var world = new World();
        var timestep = new FixedTimestep(TimeSpan.FromSeconds(0.25d), TimeSpan.FromSeconds(1d));
        var loop = new TestGameLoop(6, 0.25f, timestep);
        var steps = new List<int>();
        var frames = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new CountingSystem(steps));
        pipeline.AddFrame(new CountingFrameSystem(frames));

        loop.Run(
            update: step =>
            {
                world.Update(step, pipeline);
                timestep.Paused = true;
            },
            render: frame => world.UpdateFrame(frame, pipeline));

        steps.Should().ContainSingle("the clock stops, so the frames that follow report no step");
        frames.Should().HaveCount(6);
        timestep.Tick.Should().Be(1);
    }

    private sealed class CountingSystem : ISystem
    {
        private readonly List<int> _steps;

        public CountingSystem(List<int> steps) => _steps = steps;

        public void Update(World world, in GameTime time) => _steps.Add(1);
    }

    private sealed class CountingFrameSystem : IFrameSystem
    {
        private readonly List<int> _frames;

        public CountingFrameSystem(List<int> frames) => _frames = frames;

        public void UpdateFrame(World world, in GameTime frame) => _frames.Add(1);
    }

    [Fact]
    public void TestGameLoop_RunFixed_NullCallbacks_Throw()
    {
        var loop = new TestGameLoop(1, 0.5f);

        FluentActions.Invoking(() => loop.Run(null!, _ => { })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => loop.Run(_ => { }, null!)).Should().Throw<ArgumentNullException>();
    }
}
