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
}
