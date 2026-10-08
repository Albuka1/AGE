using Age.Assets;
using Age.Audio;
using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class GameShutdownTests
{
    [Fact]
    public void GameShutdown_Run_RunsTheStepsFromTheLowestOrderToTheHighest()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown(
        [
            new FakeStep(30, "third", log),
            new FakeStep(10, "first", log),
            new FakeStep(20, "second", log),
        ]);

        shutdown.Count.Should().Be(3);
        shutdown.Run();

        log.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void GameShutdown_Run_IsIdempotent()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown([new FakeStep(10, "once", log)]);

        shutdown.Run();
        shutdown.Run();

        log.Should().Equal("once");
    }

    [Fact]
    public void GameShutdown_Run_RunsEveryStepEvenWhenOneFails()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown(
        [
            new FakeStep(10, "first", log),
            new FakeStep(20, "second", log, new InvalidOperationException("the step failed")),
            new FakeStep(30, "third", log),
        ]);

        Action run = shutdown.Run;

        run.Should().Throw<InvalidOperationException>().WithMessage("the step failed");
        log.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void GameShutdown_Run_WithoutSteps_DoesNothing()
    {
        var shutdown = new GameShutdown([]);

        shutdown.Count.Should().Be(0);
        shutdown.Run();
    }

    [Fact]
    public void GameShutdown_StepsOfTheEngine_ComeFromEveryAssemblyThatOwnsADeviceObject()
    {
        GameShutdown shutdown = new ServiceCollection()
            .AddAgeCore()
            .AddAgeAssets()
            .AddAgeInput()
            .AddAgeAudio()
            .AddAgeRendering()
            .AddAgeSilkInput()
            .BuildServiceProvider()
            .GetRequiredService<GameShutdown>();

        shutdown.Count.Should().Be(3, "the audio assembly releases its samples, the rendering assembly its splash, its textures, its renderer and its window");
    }

    private sealed class FakeStep : IGameShutdownStep
    {
        private readonly string _name;
        private readonly List<string> _log;
        private readonly Exception? _failure;

        public FakeStep(int order, string name, List<string> log, Exception? failure = null)
        {
            Order = order;
            _name = name;
            _log = log;
            _failure = failure;
        }

        public int Order { get; }

        public void Shutdown()
        {
            _log.Add(_name);

            if (_failure is not null)
            {
                throw _failure;
            }
        }
    }
}
