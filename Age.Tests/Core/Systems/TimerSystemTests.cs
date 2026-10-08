using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TimerSystemTests
{
    private const double Step = 0.1d;

    [Fact]
    public void TimerSystem_CountsDownOverTheStepsAndRunsOutOnce()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var elapsed = new List<Entity>();
        var announced = new List<Entity>();

        world.Events.Subscribe<TimerElapsedEvent>((entity, timer) =>
        {
            // The event is raised without an entity, because it carries its own, so the world names none to the handler.
            announced.Add(entity);
            elapsed.Add(timer.Entity);
        });
        world.Set(effect, TimerComponent.For(0.25f));

        Update(world, system, 2);

        elapsed.Should().BeEmpty("a tenth of a second is left of the timer");
        world.Get<TimerComponent>(effect).Remaining.Should().BeApproximately(0.05f, 0.0001f);

        Update(world, system, 4);

        elapsed.Should().Equal(effect);
        announced.Should().OnlyContain(entity => entity == default, "the event carries the entity, so the world names none to the handler");
        TimerComponent timer = world.Get<TimerComponent>(effect);
        timer.Paused.Should().BeTrue("a timer that ran out once stops instead of removing itself");
        timer.Remaining.Should().Be(0f);
    }

    [Fact]
    public void TimerSystem_TimerThatRepeats_CarriesTheLeftoverOfTheStepIntoTheNextRun()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var runs = new List<TimerElapsedEvent>();

        world.Events.Subscribe<TimerElapsedEvent>((_, elapsed) => runs.Add(elapsed));
        world.Set(effect, TimerComponent.For(0.25f, repeat: true));

        Update(world, system, 3);

        runs.Should().HaveCount(1);
        runs[0].Repeating.Should().BeTrue();
        world.Get<TimerComponent>(effect).Remaining.Should().BeApproximately(0.2f, 0.0001f, "the next run starts from the leftover of the step that ran past the end");

        Update(world, system, 3);

        runs.Should().HaveCount(2, "the second run started from the leftover rather than from the whole duration");
        world.Get<TimerComponent>(effect).Paused.Should().BeFalse("a timer that repeats keeps running");
    }

    [Fact]
    public void TimerSystem_PausedTimer_StandsStill()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        TimerComponent timer = TimerComponent.For(0.1f);
        timer.Paused = true;

        world.Set(effect, timer);

        Update(world, system, 5);

        world.Get<TimerComponent>(effect).Remaining.Should().Be(0.1f, "a timer that stands still is not advanced, which is what cancelling one means");
    }

    [Fact]
    public void TimerSystem_HandlerThatDestroysTheEntity_LeavesTheWorldWithoutIt()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();

        world.Events.Subscribe<TimerElapsedEvent>((_, timer) => world.RequestDestroy(timer.Entity));
        world.Set(effect, TimerComponent.For(0.1f));

        Update(world, system, 1);
        world.ApplyPending();

        world.IsAlive(effect).Should().BeFalse("the world applied what the handler asked for");
        world.Has<TimerComponent>(effect).Should().BeFalse();
    }

    [Fact]
    public void TimerSystem_RunOutInTheMiddleOfAStep_ReachesTheHandlerAtTheBoundary()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var elapsed = new List<Entity>();
        var announced = new List<Entity>();

        world.Events.Subscribe<TimerElapsedEvent>((entity, timer) =>
        {
            announced.Add(entity);
            elapsed.Add(timer.Entity);
        });
        world.Set(effect, TimerComponent.For(0.1f));

        system.Update(world, new GameTime(Step, 0d));

        elapsed.Should().BeEmpty("the bus delivers at the boundary of the step, not in the middle of it");

        world.Events.Dispatch();

        elapsed.Should().Equal(effect);
        announced.Should().OnlyContain(entity => entity == default);
    }

    [Fact]
    public void TimerSystem_TimerThatRepeats_CatchesUpWhenAStepIsLongerThanItsRun()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var runs = new List<TimerElapsedEvent>();

        world.Events.Subscribe<TimerElapsedEvent>((_, elapsed) => runs.Add(elapsed));
        world.Set(effect, TimerComponent.For(0.25f, repeat: true));

        // A step of a whole second is four runs of a quarter of a second, so every one of them reports: a timer that
        // reported once and kept the rest of the step would sink below the time it counts on every such step.
        Update(world, system, 1, 1d);

        runs.Should().HaveCount(4);
        runs.Should().OnlyContain(run => run.Repeating);
        world.Get<TimerComponent>(effect).Remaining.Should().BeApproximately(0.25f, 0.0001f, "the run that began after the fourth one is whole");
    }

    [Fact]
    public void TimerSystem_TimerThatRepeatsWithoutDuration_ReportsOncePerStepAndStandsAtZero()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var runs = new List<TimerElapsedEvent>();

        world.Events.Subscribe<TimerElapsedEvent>((_, elapsed) => runs.Add(elapsed));
        world.Set(effect, TimerComponent.For(0f, repeat: true));

        Update(world, system, 3);

        runs.Should().HaveCount(3, "a run of no length is over as soon as it starts, so it reports once for each step");
        TimerComponent timer = world.Get<TimerComponent>(effect);
        timer.Remaining.Should().Be(0f, "the timer stands at zero instead of sinking by a step on every one");
        timer.Paused.Should().BeFalse();
    }

    [Fact]
    public void TimerSystem_TimerThatDoesNotRepeat_ReportsOnceEvenWhenAStepIsLongerThanItsRun()
    {
        var world = new World();
        var system = new TimerSystem();
        Entity effect = world.CreateEntity();
        var runs = new List<TimerElapsedEvent>();

        world.Events.Subscribe<TimerElapsedEvent>((_, elapsed) => runs.Add(elapsed));
        world.Set(effect, TimerComponent.For(0.25f));

        Update(world, system, 1, 1d);

        runs.Should().HaveCount(1, "a timer that does not repeat runs out once, however long the step was");
        runs[0].Repeating.Should().BeFalse();
        world.Get<TimerComponent>(effect).Paused.Should().BeTrue();
        world.Get<TimerComponent>(effect).Remaining.Should().Be(0f);
    }

    private static void Update(World world, TimerSystem system, int steps, double length = Step)
    {
        var pipeline = new SystemPipeline();
        pipeline.Add(system);

        for (var step = 0; step < steps; step++)
        {
            world.Update(new GameTime(length, step * length), pipeline);
        }
    }
}
