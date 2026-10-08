using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SystemPipelineTests
{
    [Fact]
    public void SystemPipeline_ExecutesSystemsInOrder()
    {
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new RecordingSystem(1, order));
        pipeline.Add(new RecordingSystem(2, order));

        pipeline.Update(world, new GameTime(0d, 0d));

        order.Should().Equal(1, 2);
    }

    [Fact]
    public void SystemPipeline_StepsAndFrames_AreTwoLists()
    {
        var world = new World();
        var steps = new List<int>();
        var frames = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new RecordingSystem(1, steps));
        pipeline.AddFrame(new RecordingFrameSystem(10, frames));

        pipeline.Update(world, new GameTime(0d, 0d));

        steps.Should().Equal(1);
        frames.Should().BeEmpty("a frame system does not run with the steps");

        pipeline.UpdateFrame(world, new GameTime(0.5d, 0.5d));

        steps.Should().ContainSingle("a step system does not run with the frames").Which.Should().Be(1);
        frames.Should().Equal(10);
    }

    [Fact]
    public void SystemPipeline_FrameSystems_RunInInsertionOrderAndSeeTheFrameTime()
    {
        var world = new World();
        var frames = new List<int>();
        var times = new List<GameTime>();
        var pipeline = new SystemPipeline();
        pipeline.AddFrame(new RecordingFrameSystem(1, frames));
        pipeline.AddFrame(new TimeRecordingFrameSystem(times));

        pipeline.UpdateFrame(world, new GameTime(0.25d, 7d));

        frames.Should().Equal(1);
        times.Should().ContainSingle();
        times[0].Delta.Should().Be(0.25d, "a frame system receives the time of the frame, not the fixed step");
        times[0].Total.Should().Be(7d);
    }

    [Fact]
    public void SystemPipeline_AddFrameNull_Throws() =>
        FluentActions.Invoking(() => new SystemPipeline().AddFrame(null!)).Should().Throw<ArgumentNullException>();

    [Fact]
    public void SystemPipeline_UpdateFrameNullWorld_Throws() =>
        FluentActions.Invoking(() => new SystemPipeline().UpdateFrame(null!, new GameTime(0d, 0d)))
            .Should().Throw<ArgumentNullException>();

    private sealed class RecordingSystem : ISystem
    {
        private readonly int _id;
        private readonly List<int> _order;

        public RecordingSystem(int id, List<int> order)
        {
            _id = id;
            _order = order;
        }

        public void Update(World world, in GameTime time) => _order.Add(_id);
    }

    private sealed class RecordingFrameSystem : IFrameSystem
    {
        private readonly int _id;
        private readonly List<int> _order;

        public RecordingFrameSystem(int id, List<int> order)
        {
            _id = id;
            _order = order;
        }

        public void UpdateFrame(World world, in GameTime frame) => _order.Add(_id);
    }

    private sealed class TimeRecordingFrameSystem : IFrameSystem
    {
        private readonly List<GameTime> _times;

        public TimeRecordingFrameSystem(List<GameTime> times) => _times = times;

        public void UpdateFrame(World world, in GameTime frame) => _times.Add(frame);
    }
}
