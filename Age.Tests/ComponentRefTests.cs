using System.Text;
using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ComponentRefTests
{
    [Fact]
    public void ComponentRef_ReadsAndWritesThroughValue()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = new Vector2(1f, 2f) });
        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(entity);

        component.Entity.Should().Be(entity);
        component.Value.Position.Should().Be(new Vector2(1f, 2f));

        TransformComponent value = component.Value;
        value.Position = new Vector2(30f, 40f);
        component.Value = value;

        world.Get<TransformComponent>(entity).Position.Should().Be(new Vector2(30f, 40f));
    }

    [Fact]
    public void ComponentRef_MutatesInPlaceThroughRef()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = new Vector2(1f, 2f) });
        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(entity);

        ref TransformComponent transform = ref component.Ref;
        transform.Position += new Vector2(10f, 20f);

        world.Get<TransformComponent>(entity).Position.Should().Be(new Vector2(11f, 22f));
    }

    [Fact]
    public void ComponentRef_AfterTheEntityWasDestroyed_Throws()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());
        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(entity);

        world.DestroyEntity(entity);

        Probe(component).Should().Be(Threw);
    }

    [Fact]
    public void ComponentRef_AfterTheSlotWasHandedOutAgain_ThrowsAndLeavesTheReplacementAlone()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent { Position = new Vector2(1f, 2f) });
        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(first);

        world.DestroyEntity(first);
        Entity second = world.CreateEntity();
        world.Set(second, new TransformComponent { Position = new Vector2(3f, 4f) });
        second.Id.Should().Be(first.Id);

        Probe(component).Should().Be(Threw);
        world.Get<TransformComponent>(second).Position.Should().Be(new Vector2(3f, 4f));
    }

    [Fact]
    public void ComponentRef_AfterTheComponentWasRemoved_Throws()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());
        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(entity);

        world.Remove<TransformComponent>(entity);

        Probe(component).Should().Be(ReadAndRefThrew);

        // The setter of a borrow calls Set, which attaches a component again, and the entity itself is alive.
        world.Has<TransformComponent>(entity).Should().BeTrue();
    }

    [Fact]
    public void World_BorrowWithoutTheComponent_Throws()
    {
        var world = new World();
        Entity entity = world.CreateEntity();

        FluentActions.Invoking(() => world.Borrow<TransformComponent>(entity))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void World_BorrowOfADestroyedEntity_Throws()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        world.DestroyEntity(entity);

        FluentActions.Invoking(() => world.Borrow<TransformComponent>(entity))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void World_BorrowTakenAfterTheSlotWasHandedOutAgain_SeesTheNewComponent()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent { Position = new Vector2(1f, 2f) });
        world.DestroyEntity(first);
        Entity second = world.CreateEntity();
        world.Set(second, new TransformComponent { Position = new Vector2(5f, 6f) });

        ComponentRef<TransformComponent> component = world.Borrow<TransformComponent>(second);

        component.Value.Position.Should().Be(new Vector2(5f, 6f));
    }

    [Fact]
    public void World_ReferenceHeldAcrossTheReuse_ReachesTheComponentOfTheReplacement()
    {
        var world = new World();
        Entity first = world.CreateEntity();
        world.Set(first, new TransformComponent { Position = new Vector2(1f, 2f) });
        ref TransformComponent reference = ref world.GetRef<TransformComponent>(first);

        world.DestroyEntity(first);
        Entity second = world.CreateEntity();
        world.Set(second, new TransformComponent { Position = new Vector2(3f, 4f) });

        // This is the lifetime that GetRef documents, and the reason Borrow exists: nothing checks the reference, and the
        // storage of the slot belongs to the entity that holds the slot now.
        second.Id.Should().Be(first.Id);
        reference.Position = new Vector2(99f, 99f);
        world.Get<TransformComponent>(second).Position.Should().Be(new Vector2(99f, 99f));
    }

    private const string Threw = "read:threw ref:threw write:threw";

    /// <summary>The report of a borrow of a live entity whose component was removed: the write attaches it again, like <see cref="World.Set{T}"/>.</summary>
    private const string ReadAndRefThrew = "read:threw ref:threw write:ok";

    /// <summary>
    /// Touches every access of a borrow, reading before writing so that a write cannot hide a missing component, and
    /// reports which accesses threw. A borrow is passed by value, because a ref struct cannot be captured by a lambda.
    /// </summary>
    private static string Probe(ComponentRef<TransformComponent> component)
    {
        var report = new StringBuilder();

        try
        {
            _ = component.Value;
            report.Append("read:ok ");
        }
        catch (InvalidOperationException)
        {
            report.Append("read:threw ");
        }

        try
        {
            ref TransformComponent reference = ref component.Ref;
            _ = reference.Position;
            report.Append("ref:ok ");
        }
        catch (InvalidOperationException)
        {
            report.Append("ref:threw ");
        }

        try
        {
            component.Value = new TransformComponent();
            report.Append("write:ok");
        }
        catch (InvalidOperationException)
        {
            report.Append("write:threw");
        }

        return report.ToString();
    }
}
