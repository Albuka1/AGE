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

    [Fact]
    public void CollisionSystem_UnchangedWorld_KeepsThePairs()
    {
        var world = new World();
        CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();

        system.Update(world, new GameTime(0d, 0d));
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().HaveCount(1);
    }

    [Fact]
    public void CollisionSystem_MovedBackTogether_ReportsThePairAgain()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();

        system.Update(world, new GameTime(0d, 0d));
        world.Set(first, new TransformComponent { Position = new Vector2(1024f, 1024f), Scale = new Vector2(1f, 1f) });
        system.Update(world, new GameTime(0d, 0d));
        system.LastPairs.Should().BeEmpty();

        world.Set(first, new TransformComponent { Position = new Vector2(8f, 8f), Scale = new Vector2(1f, 1f) });
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().HaveCount(1);
    }

    [Fact]
    public void CollisionSystem_DestroyedCollider_IsForgotten()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();
        system.Update(world, new GameTime(0d, 0d));

        world.DestroyEntity(first);
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().BeEmpty();
        system.CellCount.Should().Be(1);
    }

    [Fact]
    public void CollisionSystem_RemovedColliderComponent_IsForgotten()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        CreateBox(world, new Vector2(16f, 16f));
        var system = new CollisionSystem();
        system.Update(world, new GameTime(0d, 0d));

        world.Remove<ColliderComponent>(first);
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().BeEmpty();
    }

    [Fact]
    public void CollisionSystem_ColliderAddedLater_IsPickedUp()
    {
        var world = new World();
        CreateBox(world, new Vector2(0f, 0f));
        Entity late = world.CreateEntity();
        world.Set(late, new TransformComponent { Position = new Vector2(16f, 16f), Scale = new Vector2(1f, 1f) });
        var system = new CollisionSystem();
        system.Update(world, new GameTime(0d, 0d));
        system.LastPairs.Should().BeEmpty();

        world.Set(late, new ColliderComponent { Size = new Vector2(32f, 32f) });
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().HaveCount(1);
    }

    [Fact]
    public void CollisionSystem_ReusedIdentifier_DoesNotPairWithTheBoxOfTheOldEntity()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        Entity second = CreateBox(world, new Vector2(900f, 900f));
        var system = new CollisionSystem();
        system.Update(world, new GameTime(0d, 0d));
        system.LastPairs.Should().BeEmpty();

        world.DestroyEntity(first);
        Entity again = CreateBox(world, new Vector2(904f, 904f));
        again.Id.Should().Be(first.Id);
        system.Update(world, new GameTime(0d, 0d));

        system.LastPairs.Should().ContainSingle();
        system.LastPairs[0].A.Should().Be(again);
        system.LastPairs[0].B.Should().Be(second);
    }

    [Fact]
    public void CollisionSystem_EmptyCells_AreDropped()
    {
        var world = new World();
        Entity first = CreateBox(world, new Vector2(0f, 0f));
        var system = new CollisionSystem();
        system.Update(world, new GameTime(0d, 0d));
        system.CellCount.Should().Be(1);

        world.Set(first, new TransformComponent { Position = new Vector2(4096f, 4096f), Scale = new Vector2(1f, 1f) });
        system.Update(world, new GameTime(0d, 0d));

        system.CellCount.Should().Be(1);
    }

    [Fact]
    public void CollisionSystem_MovingWorld_MatchesABruteForceScan()
    {
        var world = new World();
        var boxes = new List<Entity>();
        var system = new CollisionSystem();

        for (int index = 0; index < 8; index++)
        {
            boxes.Add(CreateBox(world, new Vector2(index * 30f, index * 20f)));
        }

        for (int frame = 0; frame < 24; frame++)
        {
            Entity mover = boxes[frame % boxes.Count];
            world.Set(mover, new TransformComponent
            {
                Position = new Vector2(MathF.Cos(frame) * 90f, MathF.Sin(frame) * 90f),
                Scale = new Vector2(1f, 1f),
            });

            if (frame == 9)
            {
                world.DestroyEntity(boxes[3]);
                boxes[3] = CreateBox(world, new Vector2(45f, 45f));
            }

            if (frame == 15)
            {
                world.Remove<ColliderComponent>(boxes[5]);
            }

            system.Update(world, new GameTime(0d, 0d));

            Pairs(system).Should().Equal(Overlaps(world), "frame {0}", frame);
        }
    }

    /// <summary>The pairs that the system reported, ordered by slot and generation.</summary>
    private static List<(Entity A, Entity B)> Pairs(CollisionSystem system) =>
        [.. system.LastPairs.Select(pair => (pair.A, pair.B)).OrderBy(pair => pair.A.Id).ThenBy(pair => pair.B.Id)];

    /// <summary>The overlapping pairs of a plain scan of every collider, in the same order, as the reference of the hash.</summary>
    private static List<(Entity A, Entity B)> Overlaps(World world)
    {
        List<Entity> boxes =
        [
            .. world.Enumerate<ColliderComponent>().Where(entity => world.Has<TransformComponent>(entity)),
        ];
        var pairs = new List<(Entity A, Entity B)>();

        for (int first = 0; first < boxes.Count; first++)
        {
            for (int second = first + 1; second < boxes.Count; second++)
            {
                Entity a = boxes[first];
                Entity b = boxes[second];

                if (a.Id > b.Id)
                {
                    (a, b) = (b, a);
                }

                if (BoxOf(world, a).Intersects(BoxOf(world, b)))
                {
                    pairs.Add((a, b));
                }
            }
        }

        return pairs;
    }

    private static Aabb BoxOf(World world, Entity entity)
    {
        TransformComponent transform = world.Get<TransformComponent>(entity);
        ColliderComponent collider = world.Get<ColliderComponent>(entity);
        return Aabb.FromRect(new Rect(transform.Position + collider.Offset, collider.Size * transform.Scale));
    }

    private static Entity CreateBox(World world, Vector2 position)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = position, Scale = new Vector2(1f, 1f) });
        world.Set(entity, new ColliderComponent { Size = new Vector2(32f, 32f) });
        return entity;
    }
}
