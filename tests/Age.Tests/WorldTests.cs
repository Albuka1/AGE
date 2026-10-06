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
    public void World_EntityIdNotReused()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.DestroyEntity(first);

        Entity second = world.CreateEntity();

        second.Id.Should().NotBe(first.Id);
    }
}
