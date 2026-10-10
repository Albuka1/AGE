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

    [Fact]
    public void SystemPipeline_Update_RecordsTheTimeEveryStepSystemSpent()
    {
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new RecordingSystem(1, order));
        pipeline.Add(new RecordingSystem(2, order));

        pipeline.Update(world, new GameTime(0d, 0d));

        pipeline.StepTimings.Should().HaveCount(2);
        pipeline.StepTimings.Should().OnlyContain(timing => timing.Name == nameof(RecordingSystem));
        pipeline.StepTimings.Should().OnlyContain(timing => timing.Milliseconds >= 0d);
    }

    [Fact]
    public void SystemPipeline_UpdateFrame_RecordsTheTimeEveryFrameSystemSpentAndLeavesTheStepTimings()
    {
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new RecordingSystem(1, order));
        pipeline.AddFrame(new RecordingFrameSystem(2, order));

        pipeline.Update(world, new GameTime(0d, 0d));
        pipeline.UpdateFrame(world, new GameTime(0d, 0d));

        pipeline.FrameTimings.Should().ContainSingle().Which.Name.Should().Be(nameof(RecordingFrameSystem));
        pipeline.StepTimings.Should().ContainSingle().Which.Name.Should().Be(nameof(RecordingSystem), "the frame does not disturb what the step recorded");
    }

    [Fact]
    public void SystemPipeline_ASystemThatIsOff_IsSkippedAndKeepsItsPlace()
    {
        // A paused menu and a developer overlay turn a rule off with this, so a rule that is off costs a property read and
        // nothing else: the system keeps its state and its position, and it starts running again where the order says.
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        var first = new RecordingSystem(1, order);
        var second = new RecordingSystem(2, order);
        pipeline.Add(first);
        pipeline.Add(second);

        first.Enabled = false;
        pipeline.Update(world, new GameTime(0d, 0d));

        order.Should().Equal([2], "the system that is off does not run");
        pipeline.StepTimings.Should().ContainSingle("a system that was skipped did not spend time").Which.Name.Should().Be(nameof(RecordingSystem));

        first.Enabled = true;
        pipeline.Update(world, new GameTime(0d, 0d));

        order.Should().Equal([2, 1, 2], "the system that is on again runs in its own place in the order");
    }

    [Fact]
    public void SystemPipeline_AFrameSystemThatIsOff_IsSkippedAndKeepsItsPlace()
    {
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        var hidden = new RecordingFrameSystem(1, order);
        pipeline.AddFrame(hidden);
        pipeline.AddFrame(new RecordingFrameSystem(2, order));

        hidden.Enabled = false;
        pipeline.UpdateFrame(world, new GameTime(0d, 0d));

        order.Should().Equal([2], "a frame system that is off stops drawing itself");
        pipeline.FrameTimings.Should().ContainSingle().Which.Name.Should().Be(nameof(RecordingFrameSystem));

        hidden.Enabled = true;
        pipeline.UpdateFrame(world, new GameTime(0d, 0d));

        order.Should().Equal([2, 1, 2]);
    }

    [Fact]
    public void SystemPipeline_ASystemOfAGame_IsOnUnlessItSaysOtherwise()
    {
        // The interface answers true by default, so a system that knows nothing about being switched off is a system that runs.
        var world = new World();
        var order = new List<int>();
        var pipeline = new SystemPipeline();
        pipeline.Add(new RecordingSystem(1, order));

        order.Should().BeEmpty();

        pipeline.Update(world, new GameTime(0d, 0d));

        order.Should().Equal(1);
    }

    private sealed class RecordingSystem : ISystem
    {
        private readonly int _id;
        private readonly List<int> _order;

        public RecordingSystem(int id, List<int> order)
        {
            _id = id;
            _order = order;
        }

        public bool Enabled { get; set; } = true;

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

        public bool Enabled { get; set; } = true;

        public void UpdateFrame(World world, in GameTime frame) => _order.Add(_id);
    }

    private sealed class TimeRecordingFrameSystem : IFrameSystem
    {
        private readonly List<GameTime> _times;

        public TimeRecordingFrameSystem(List<GameTime> times) => _times = times;

        public void UpdateFrame(World world, in GameTime frame) => _times.Add(frame);
    }
}
