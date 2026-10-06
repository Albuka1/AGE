using Age.Core;
using Age.Physics;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class CollisionSystemTests
{
    [Fact]
    public void CollisionSystem_OverlappingColliders_ProducesPair()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        Entity second = CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();

        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().HaveCount(1);
        system.LastPairs[0].A.Id.Should().Be(Math.Min(first.Id, second.Id));
        system.LastPairs[0].B.Id.Should().Be(Math.Max(first.Id, second.Id));
        world.Has<CollisionComponent>(first).Should().BeTrue();
        world.Has<CollisionComponent>(second).Should().BeTrue();
    }

    [Fact]
    public void CollisionSystem_NonOverlappingColliders_ProducesEmpty()
    {
        var world = new World();
        CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(128f, 128f));
        var system = new CollisionSystem();

        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().BeEmpty();
    }

    [Fact]
    public void CollisionSystem_AfterMove_RecalculatesPairs()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();

        system.Update(world, new GameTime(0d, 0d));
        system.LastPairs.Should().HaveCount(1);

        world.Set(first, new TransformComponent { Position = new Vector2(1000f, 1000f), Scale = new Vector2(1f, 1f) });
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().BeEmpty();
        world.Has<CollisionComponent>(first).Should().BeFalse();
    }

    private static Entity CreateBox(World world, Vector2 position)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = position, Scale = new Vector2(1f, 1f) });
        world.Set(entity, new ColliderComponent { Size = new Vector2(32f, 32f) });
        return entity;
    }
}
