using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class EntityRefTests
{
    [Fact]
    public void EntityRef_None_NamesNoEntityAndResolvesToNothing()
    {
        var world = new World();

        EntityRef none = EntityRef.None;

        none.HasValue.Should().BeFalse();
        none.SceneId.Should().Be(0);
        none.Resolve(world).Should().Be(default(Entity));
        none.IsAlive(world).Should().BeFalse();
        new EntityRef(0).Should().Be(none);
    }

    [Fact]
    public void EntityRef_ReferenceToAnEntity_ResolvesToItInTheWorldThatMadeIt()
    {
        var world = new World();
        Entity entity = world.CreateEntity();

        EntityRef reference = world.Reference(entity);

        reference.HasValue.Should().BeTrue();
        reference.SceneId.Should().Be(world.SceneIdOf(entity));
        reference.Resolve(world).Should().Be(entity);
        reference.IsAlive(world).Should().BeTrue();
        world.Resolve(reference).Should().Be(entity);
        world.Reference(entity).Should().Be(reference);
    }

    [Fact]
    public void EntityRef_ReferenceToADestroyedEntity_NamesNoEntity()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        EntityRef reference = world.Reference(entity);

        world.DestroyEntity(entity);

        reference.IsAlive(world).Should().BeFalse("the world does not know the identifier any more");
        reference.Resolve(world).Should().Be(default(Entity));
        world.SceneIdOf(entity).Should().Be(0);
        world.Reference(entity).Should().Be(EntityRef.None, "a world hands out no reference to an entity it does not hold");
    }

    [Fact]
    public void EntityRef_SameEntityTwoWorlds_DiffersBecauseTheIdentifiersDifferPerWorld()
    {
        var first = new World();
        var second = new World();
        Entity inFirst = first.CreateEntity();
        Entity inSecond = second.CreateEntity();

        first.Reference(inFirst).Should().Be(second.Reference(inSecond), "both worlds hand out their first identifier");
        first.Reference(inFirst).Resolve(second).Should().Be(inSecond, "an identifier is read by the world that holds it");
    }
}
