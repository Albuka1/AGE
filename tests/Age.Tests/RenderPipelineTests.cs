using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class RenderPipelineTests
{
    [Fact]
    public void RenderPipeline_RunsPassesInOrderAndHandsThemTheWorldAndTheCamera()
    {
        var world = new World();
        var camera = new Camera2D { Zoom = 2f };
        var pipeline = new RenderPipeline();
        var calls = new List<string>();
        var first = new RecordingPass("first", calls);
        var second = new RecordingPass("second", calls);
        pipeline.Add(first);
        pipeline.Add(second);

        pipeline.Render(world, camera);

        pipeline.Count.Should().Be(2);
        calls.Should().Equal("first", "second");
        first.SeenWorld.Should().BeSameAs(world);
        first.SeenZoom.Should().Be(2f);
        second.SeenWorld.Should().BeSameAs(world);
    }

    [Fact]
    public void RenderPipeline_WithoutPasses_DoesNothing()
    {
        var pipeline = new RenderPipeline();

        FluentActions.Invoking(() => pipeline.Render(new World(), new Camera2D { Zoom = 1f })).Should().NotThrow();

        pipeline.Count.Should().Be(0);
    }

    [Fact]
    public void RenderPipeline_AddNull_Throws() =>
        FluentActions.Invoking(() => new RenderPipeline().Add(null!)).Should().Throw<ArgumentNullException>();

    [Fact]
    public void RenderPipeline_RenderNullWorld_Throws()
    {
        var pipeline = new RenderPipeline();

        FluentActions.Invoking(() => pipeline.Render(null!, new Camera2D { Zoom = 1f }))
            .Should().Throw<ArgumentNullException>();
    }

    private sealed class RecordingPass : IRenderPass
    {
        private readonly List<string> _calls;
        private readonly string _name;

        public RecordingPass(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public World? SeenWorld { get; private set; }

        public float SeenZoom { get; private set; }

        public void Render(World world, in Camera2D camera)
        {
            SeenWorld = world;
            SeenZoom = camera.Zoom;
            _calls.Add(_name);
        }
    }
}
