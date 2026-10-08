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
}
