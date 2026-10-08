using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class WorldTests
{
    [Fact]
    public void World_CreateEntity_IsAliveTrue()
    {
        var world = new World();

        Entity entity = world.CreateEntity();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void World_DestroyEntity_IsAliveFalse()
    {
        var world = new World();
        Entity entity = world.CreateEntity();

        world.DestroyEntity(entity);

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void World_GetAfterDestroy_ThrowsInvalidOperationException()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());
        world.DestroyEntity(entity);

        Action act = () => world.Get<TransformComponent>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void World_CreateEntity_IdentifiesTheFirstSlot()
    {
        var world = new World();

        Entity entity = world.CreateEntity();

        entity.Id.Should().Be(0);
        entity.Generation.Should().Be(1);
    }

    [Fact]
    public void World_DestroyedSlot_IsHandedOutAgainInANewGeneration()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.DestroyEntity(first);

        Entity second = world.CreateEntity();

        second.Id.Should().Be(first.Id);
        second.Generation.Should().NotBe(first.Generation);
        second.Should().NotBe(first);
    }

    [Fact]
    public void World_IdentifierOfADestroyedEntity_IsNotAlive()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.DestroyEntity(first);

        Entity second = world.CreateEntity();

        world.IsAlive(first).Should().BeFalse();
        world.IsAlive(second).Should().BeTrue();
        FluentActions.Invoking(() => world.Get<TransformComponent>(first)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => world.Set(first, new TransformComponent())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void World_DefaultIdentifier_IsNotAlive()
    {
        var world = new World();

        world.IsAlive(default).Should().BeFalse();
    }

    [Fact]
    public void World_ReusedSlot_DoesNotKeepTheComponentsOfTheOldEntity()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent { Position = new Vector2(4f, 8f) });
        world.DestroyEntity(first);

        Entity second = world.CreateEntity();

        second.Id.Should().Be(first.Id);
        world.Has<TransformComponent>(second).Should().BeFalse();
        world.Has<TransformComponent>(first).Should().BeFalse();
    }

    [Fact]
    public void World_Enumerate_ReportsTheGenerationOfTheLiveEntity()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent());
        world.DestroyEntity(first);
        Entity second = world.CreateEntity();
        world.Set(second, new TransformComponent());

        world.Enumerate().Should().Equal(second);
        world.Enumerate<TransformComponent>().Should().Equal(second);
        world.Enumerate().Should().OnlyContain(entity => world.IsAlive(entity));
    }

    [Fact]
    public void World_RemoveWithTheIdentifierOfADestroyedEntity_LeavesTheReusedSlotAlone()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent { Position = new Vector2(4f, 8f) });
        world.DestroyEntity(first);
        Entity second = world.CreateEntity();
        world.Set(second, new TransformComponent { Position = new Vector2(1f, 2f) });

        world.Remove<TransformComponent>(first);

        world.Has<TransformComponent>(second).Should().BeTrue();
        world.Get<TransformComponent>(second).Position.Should().Be(new Vector2(1f, 2f));
    }

    [Fact]
    public void World_RemoveOfAComponentTheEntityDoesNotHave_DoesNothing()
    {
        var world = new World();
        Entity withTransform = world.CreateEntity();
        world.Set(withTransform, new TransformComponent());
        Entity withoutTransform = world.CreateEntity();

        FluentActions.Invoking(() => world.Remove<TransformComponent>(withoutTransform)).Should().NotThrow();

        world.Has<TransformComponent>(withTransform).Should().BeTrue();
        world.Has<TransformComponent>(withoutTransform).Should().BeFalse();
    }

    [Fact]
    public void World_RequestDestroy_ReportsTheEntityAsGoneBeforeItIsApplied()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());

        world.RequestDestroy(entity);

        world.IsAlive(entity).Should().BeFalse("the request is a promise for the end of the step");
        world.Has<TransformComponent>(entity).Should().BeFalse();

        world.ApplyPending().Should().Be(1);
        world.IsAlive(entity).Should().BeFalse();
        world.Has<TransformComponent>(entity).Should().BeFalse();
    }

    [Fact]
    public void World_RequestDestroy_LeavesAnEnumerationIntact()
    {
        var world = new World();
        for (int index = 0; index < 5; index++)
        {
            world.Set(world.CreateEntity(), new TransformComponent());
        }

        var visited = new List<Entity>();

        foreach (Entity entity in world.Enumerate<TransformComponent>())
        {
            visited.Add(entity);
            world.RequestDestroy(entity);
        }

        visited.Should().HaveCount(5, "the whole enumeration ran to its end");
        world.ApplyPending().Should().Be(5);
        world.Enumerate<TransformComponent>().Should().BeEmpty("every entity that was requested is gone");
    }

    [Fact]
    public void World_DestroyDuringEnumeration_Throws()
    {
        var world = new World();
        world.Set(world.CreateEntity(), new TransformComponent());

        Action act = () =>
        {
            foreach (Entity entity in world.Enumerate<TransformComponent>())
            {
                world.DestroyEntity(entity);
            }
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*while the world was being enumerated*");
    }

    [Fact]
    public void World_ComponentThatAppearsDuringEnumeration_Throws()
    {
        var world = new World();
        world.Set(world.CreateEntity(), new TransformComponent());
        Entity without = world.CreateEntity();

        Action act = () =>
        {
            foreach (Entity entity in world.Enumerate<TransformComponent>())
            {
                world.Set(without, new TransformComponent());
            }
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*added to or removed*");
    }

    [Fact]
    public void World_OtherComponentTypeDuringEnumeration_DoesNotThrow()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());

        Action act = () =>
        {
            foreach (Entity current in world.Enumerate<TransformComponent>())
            {
                world.Set(current, new TestComponent { Value = 1 });
            }
        };

        act.Should().NotThrow("a sequence walks the storage of one component type, and another type does not touch it");
        world.Get<TestComponent>(entity).Value.Should().Be(1);
    }

    [Fact]
    public void World_OverwritingAComponentDuringEnumeration_DoesNotThrow()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());

        Action act = () =>
        {
            foreach (Entity current in world.Enumerate<TransformComponent>())
            {
                world.Set(current, new TransformComponent { Position = new Vector2(1f, 1f) });
            }
        };

        act.Should().NotThrow("writing over a component that is there changes nothing about the enumeration");
        world.Get<TransformComponent>(entity).Position.Should().Be(new Vector2(1f, 1f));
    }

    [Fact]
    public void World_Requests_AreAppliedInTheOrderTheyWereMade()
    {
        var world = new World();
        Entity entity = world.CreateEntity();

        world.RequestSet(entity, new TransformComponent { Position = new Vector2(1f, 0f) });
        world.RequestRemove<TransformComponent>(entity);
        world.ApplyPending();

        world.Has<TransformComponent>(entity).Should().BeFalse("the removal was requested last");

        world.RequestRemove<TransformComponent>(entity);
        world.RequestSet(entity, new TransformComponent { Position = new Vector2(2f, 0f) });
        world.ApplyPending();

        world.Get<TransformComponent>(entity).Position.Should().Be(new Vector2(2f, 0f), "the write was requested last");
    }

    [Fact]
    public void World_WritingToAnEntityThatIsDestroyedInTheSameBatch_IsDropped()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());

        world.RequestDestroy(entity);
        world.RequestSet(entity, new TransformComponent { Position = new Vector2(5f, 5f) });

        Action act = () => world.ApplyPending();

        act.Should().NotThrow("the destruction wins over the write that followed it");
        world.IsAlive(entity).Should().BeFalse();
    }
    [Fact]
    public void World_ApplyPending_AppliesOnceAndReportsNothingAfterwards()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        Entity second = world.CreateEntity();

        world.RequestSet(first, new TransformComponent());
        world.RequestRemove<TransformComponent>(second);

        world.ApplyPending().Should().Be(2);
        world.ApplyPending().Should().Be(0, "nothing was requested since the last call");
    }

    [Fact]
    public void World_Update_AppliesTheRequestsOfTheSystems()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());
        var pipeline = new SystemPipeline();
        pipeline.Add(new DestroyRequestingSystem(entity));

        world.Update(new GameTime(0d, 0d), pipeline);

        world.IsAlive(entity).Should().BeFalse("the request of the step was applied at its end");
    }

    [Fact]
    public void World_RequestCreate_ReservesAnIdentifierAndCreatesTheEntityWhenApplied()
    {
        var world = new World();

        Entity entity = world.RequestCreate();

        world.IsAlive(entity).Should().BeFalse("the entity exists when the queue is applied");
        world.Enumerate().Should().BeEmpty();

        world.ApplyPending().Should().Be(1);

        world.IsAlive(entity).Should().BeTrue();
        world.Enumerate().Should().ContainSingle();
    }

    [Fact]
    public void World_RequestCreate_LeavesAnEnumerationOfTheWorldIntact()
    {
        var world = new World();
        world.Set(world.CreateEntity(), new TransformComponent());
        var created = new List<Entity>();

        Action act = () =>
        {
            foreach (Entity entity in world.Enumerate())
            {
                created.Add(world.RequestCreate());
            }
        };

        act.Should().NotThrow("the request reserves an identifier without changing the storage that is being walked");
        created.Should().HaveCount(1, "the sequence visited the entities that existed when it started");

        world.ApplyPending();

        world.Enumerate().Should().HaveCount(2);
    }

    [Fact]
    public void World_Enumerate_SkipsEntitiesWhoseDestructionWasRequested()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        Entity second = world.CreateEntity();
        world.Set(first, new TransformComponent());
        world.Set(second, new TransformComponent());

        world.RequestDestroy(first);

        world.Enumerate().Should().ContainSingle().Which.Should().Be(second);
        world.Enumerate<TransformComponent>().Should().ContainSingle().Which.Should().Be(second);
    }

    [Fact]
    public void World_CancelledReservation_KeepsItsIdentifierStale()
    {
        var world = new World();

        Entity cancelled = world.RequestCreate();
        world.DestroyEntity(cancelled);

        Entity replacement = world.RequestCreate();

        replacement.Id.Should().Be(cancelled.Id, "the cancelled slot went back to the pool");
        replacement.Generation.Should().NotBe(cancelled.Generation, "the identifier that was reserved stays stale");

        world.ApplyPending();

        world.IsAlive(replacement).Should().BeTrue("a stale creation must not consume the reservation of the new one");
        world.IsAlive(cancelled).Should().BeFalse();
        world.Enumerate().Should().ContainSingle();
    }

    [Fact]
    public void World_StaleIdentifier_DoesNotCancelOrDestroyTheNewReservation()
    {
        var world = new World();

        Entity cancelled = world.RequestCreate();
        world.DestroyEntity(cancelled);
        Entity replacement = world.RequestCreate();

        world.DestroyEntity(cancelled);
        world.RequestDestroy(cancelled);

        world.ApplyPending();

        world.IsAlive(replacement).Should().BeTrue("the stale identifier does not name the entity that holds the slot now");
        world.Enumerate().Should().ContainSingle();
    }

    private struct TestComponent : IComponent
    {
        public int Value;
    }

    private sealed class DestroyRequestingSystem : ISystem
    {
        private readonly Entity _entity;

        public DestroyRequestingSystem(Entity entity) => _entity = entity;

        public void Update(World world, in GameTime time) => world.RequestDestroy(_entity);
    }
}
