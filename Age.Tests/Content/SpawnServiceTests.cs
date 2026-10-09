using Age.Assets;
using Age.Content;
using Age.Content.Prototypes;
using Age.Core;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class SpawnServiceTests
{
    [Fact]
    public void SpawnService_Spawn_CreatesAnEntityThatCarriesWhatThePrototypeDeclares()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        Entity goblin = spawner.Spawn(world, "Goblin");

        world.IsAlive(goblin).Should().BeTrue();
        world.Has<TransformComponent>(goblin).Should().BeTrue("a goblin is a creature, and a creature has a transform");
        world.Get<TransformComponent>(goblin).Scale.Should().Be(new Vector2(1f, 1f), "the transform comes from the parent of the goblin");
        world.Has<ColliderComponent>(goblin).Should().BeTrue();
        world.Get<ColliderComponent>(goblin).Size.Should().Be(new Vector2(24f, 24f), "the goblin declares a collider of its own");
        world.Enumerate().Should().ContainSingle();
    }

    [Fact]
    public void SpawnService_Spawn_PlacesTheEntityWhereTheCallerAsked()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        Entity goblin = spawner.Spawn(world, "Goblin", new Vector2(320f, 240f));

        world.Get<TransformComponent>(goblin).Position.Should().Be(new Vector2(320f, 240f), "where a spawn puts a thing wins over where its prototype says it lies");
    }

    [Fact]
    public void SpawnService_Spawn_AnIdentifierThatIsNotThere_IsReported()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        Action spawn = () => spawner.Spawn(world, "Dragon");

        spawn.Should().Throw<KeyNotFoundException>().WithMessage("*Dragon*");
        spawner.TrySpawn(world, "Dragon", out Entity missing).Should().BeFalse();
        missing.Should().Be(default(Entity));
        world.Enumerate().Should().BeEmpty("nothing was created for a prototype the content does not hold");
    }

    [Fact]
    public void SpawnService_Spawn_MakesOneEntityPerCall()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        List<Entity> spawned = [.. Enumerable.Range(0, 100).Select(_ => spawner.Spawn(world, "Goblin"))];

        spawned.Should().OnlyHaveUniqueItems();
        world.Enumerate<ColliderComponent>().Should().HaveCount(100, "a map of a hundred units is a hundred spawns");
        world.Get<ColliderComponent>(spawned[0]).Size.Should().Be(new Vector2(24f, 24f));
        world.Get<ColliderComponent>(spawned[99]).Size.Should().Be(new Vector2(24f, 24f));
    }

    [Fact]
    public void SpawnService_Spawn_AnItemOfTheContent_IsMadeOfItsOwnFile()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        Entity sword = spawner.Spawn(world, "Sword");

        world.Get<TransformComponent>(sword).Scale.Should().Be(new Vector2(0.5f, 0.5f), "an item lies smaller than a creature");
        world.Get<ColliderComponent>(sword).Size.Should().Be(new Vector2(16f, 16f));
    }

    /// <summary>Builds a provider whose content is the one the engine ships.</summary>
    private static ServiceProvider Create()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddAgeCore()
            .AddAgeContent()
            .AddAgePhysics()
            .AddAgeUI()
            .AddAgeRendering()
            .BuildServiceProvider();

        PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
        prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);

        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        prototypes.Load(assets, "Prototypes");

        return provider;
    }
}
