using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TweenSystemTests
{
    private const double Step = 0.1d;

    [Fact]
    public void TweenSystem_LinearTween_ReportsEveryStepAndCompletesOnce()
    {
        var world = new World();
        var system = new TweenSystem();
        Entity sprite = world.CreateEntity();
        var updates = new List<TweenUpdatedEvent>();

        world.Events.Subscribe<TweenUpdatedEvent>((_, update) => updates.Add(update));
        world.Set(sprite, TweenComponent.Between(0f, 10f, 0.3f));

        Update(world, system, 2);

        updates.Should().HaveCount(2, "a tween reports every step it moves");
        updates[^1].Value.Should().BeApproximately(10f * (0.2f / 0.3f), 0.0001f);
        updates[^1].Completed.Should().BeFalse();

        Update(world, system, 2);

        updates.Should().HaveCount(3, "the tween reports its end once and then stops");
        updates[^1].Value.Should().Be(10f, "the tween reached its end");
        updates[^1].Completed.Should().BeTrue();
    }

    [Fact]
    public void TweenSystem_LoopingTween_StartsOverAndNeverCompletes()
    {
        var world = new World();
        var system = new TweenSystem();
        Entity sprite = world.CreateEntity();
        var updates = new List<TweenUpdatedEvent>();

        world.Events.Subscribe<TweenUpdatedEvent>((_, update) => updates.Add(update));
        world.Set(sprite, TweenComponent.Between(0f, 10f, 0.3f, looping: true));

        Update(world, system, 4);

        updates.Should().HaveCount(4);
        updates.Should().OnlyContain(update => !update.Completed);
        updates[^1].Progress.Should().BeApproximately(0.1f / 0.3f, 0.0001f, "the fourth step landed past the end of the third run");
        world.Get<TweenComponent>(sprite).Elapsed.Should().BeApproximately(0.1f, 0.0001f);
    }

    [Fact]
    public void TweenSystem_PausedTween_HoldsItsValue()
    {
        var world = new World();
        var system = new TweenSystem();
        Entity sprite = world.CreateEntity();
        TweenComponent tween = TweenComponent.Between(0f, 10f, 0.3f);
        tween.Paused = true;

        world.Set(sprite, tween);

        Update(world, system, 5);

        world.Get<TweenComponent>(sprite).Value.Should().Be(0f, "a tween that stands still keeps the value it had");
        world.Get<TweenComponent>(sprite).Elapsed.Should().Be(0f);
    }

    [Fact]
    public void TweenSystem_HandlerThatWritesTheValue_WritesIntoTheComponentOfTheEntity()
    {
        var world = new World();
        var system = new TweenSystem();
        Entity sprite = world.CreateEntity();

        world.Set(sprite, new TransformComponent());
        world.Set(sprite, TweenComponent.Between(0f, 1f, 0.2f));
        world.Events.Subscribe<TweenUpdatedEvent>((_, update) => world.GetRef<TransformComponent>(update.Entity).Rotation = update.Value);

        Update(world, system, 2);

        world.Get<TransformComponent>(sprite).Rotation.Should().Be(1f, "the game wrote the value of the tween into the component it belongs to");
    }

    [Fact]
    public void TweenComponent_Easing_MovesTheValueWithoutMovingTheEnds()
    {
        TweenComponent linear = TweenComponent.Between(0f, 100f, 1f);
        TweenComponent easeIn = TweenComponent.Between(0f, 100f, 1f, easing: TweenEasing.EaseIn);
        TweenComponent easeOut = TweenComponent.Between(0f, 100f, 1f, easing: TweenEasing.EaseOut);
        TweenComponent easeInOut = TweenComponent.Between(0f, 100f, 1f, easing: TweenEasing.EaseInOut);

        (linear with { Elapsed = 0.5f }).Value.Should().Be(50f);
        (easeIn with { Elapsed = 0.5f }).Value.Should().Be(25f, "the value starts slowly");
        (easeOut with { Elapsed = 0.5f }).Value.Should().Be(75f, "the value starts quickly");
        (easeInOut with { Elapsed = 0.5f }).Value.Should().Be(50f);

        (easeIn with { Elapsed = 0f }).Value.Should().Be(0f);
        (easeIn with { Elapsed = 1f }).Value.Should().Be(100f, "every easing reaches the same end");
        (easeIn with { Elapsed = 4f }).Value.Should().Be(100f, "the value stays at the end once the run is over");
        linear.Progress.Should().Be(0f);
        (linear with { Duration = 0f }).Progress.Should().Be(1f, "a tween of no length is over as soon as it starts");
    }

    private static void Update(World world, TweenSystem system, int steps)
    {
        var pipeline = new SystemPipeline();
        pipeline.Add(system);

        for (var step = 0; step < steps; step++)
        {
            world.Update(new GameTime(Step, step * Step), pipeline);
        }
    }
}
