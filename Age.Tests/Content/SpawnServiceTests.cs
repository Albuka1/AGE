using Age.Assets;
using Age.Content;
using Age.Content.Prototypes;
using Age.Content.Yaml;
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

    [Fact]
    public void SpawnService_Spawn_APrototypeWhoseComponentCannotBeRead_LeavesTheWorldAsItWas()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeCore()
            .AddAgeContent()
            .AddAgePhysics()
            .AddAgeUI()
            .AddAgeRendering()
            .BuildServiceProvider();

        // A game reads its content with the kind it registered, and it is free to hand back something else: this one answers
        // every document with a prototype that names a component the engine does not know, which is what a spawn refuses.
        var ghost = new EntityPrototype(new Prototype(
            "Ghost",
            EntityPrototype.Kind,
            parent: null,
            "Prototypes/Entities/ghost.yml",
            line: 3,
            [new PrototypeComponent("Nowhere", YamlJson.Write(YamlReader.Read("Level: 3\n", "ghost.yml")), "Prototypes/Entities/ghost.yml", 5)]));

        PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
        prototypes.Register(EntityPrototype.Kind, _ => ghost);

        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        prototypes.Load(assets, "Prototypes");

        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();

        Action spawn = () => spawner.Spawn(world, "Goblin", new Vector2(320f, 240f));

        spawn.Should().Throw<InvalidOperationException>().WithMessage("*Nowhere*");
        world.Enumerate().Should().BeEmpty("half of a creature is worse than none of it");
    }

    [Fact]
    public void SpawnService_Spawn_APrototypeWithASprite_GivesTheEntityThePathOfItsImage()
    {
        string root = Path.Combine(Path.GetTempPath(), "age-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes", "Entities"));

        try
        {
            File.WriteAllText(
                Path.Combine(root, "Prototypes", "Entities", "goblin.yml"),
                "- type: entity\n"
                + "  id: Goblin\n"
                + "  components:\n"
                + "    - type: Transform\n"
                + "      Position:\n"
                + "        X: 0\n"
                + "        Y: 0\n"
                + "    - type: Sprite\n"
                + "      TexturePath: Textures/Tiles/tiles.bmp\n"
                + "      Size:\n"
                + "        X: 32\n"
                + "        Y: 32\n");

            using ServiceProvider provider = new ServiceCollection()
                .AddAgeCore()
                .AddAgeContent()
                .AddAgePhysics()
                .AddAgeUI()
                .AddAgeRendering()
                .BuildServiceProvider();

            PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
            prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);

            var assets = new NullAssetLoader();
            assets.Initialize(root);
            prototypes.Load(assets, "Prototypes");

            SpawnService spawner = provider.GetRequiredService<SpawnService>();
            var world = new World();

            Entity goblin = spawner.Spawn(world, "Goblin");

            world.Get<SpriteComponent>(goblin).TexturePath.Should().Be("Textures/Tiles/tiles.bmp", "content names the image of a sprite by path, which is what survives a save");
            world.Get<SpriteComponent>(goblin).Texture.Id.Should().Be(0, "the texture service resolves the path when the sprite is drawn for the first time");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SpawnService_ContentThatNamesTheHandleOfATexture_LoadsWithoutIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "age-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes"));

        try
        {
            File.WriteAllText(
                Path.Combine(root, "Prototypes", "sprite.yml"),
                "- type: entity\n"
                + "  id: Broken\n"
                + "  components:\n"
                + "    - type: Sprite\n"
                + "      Texture: 7\n");

            using ServiceProvider provider = new ServiceCollection()
                .AddAgeCore()
                .AddAgeContent()
                .AddAgePhysics()
                .AddAgeUI()
                .AddAgeRendering()
                .BuildServiceProvider();

            PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
            prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);

            var assets = new NullAssetLoader();
            assets.Initialize(root);
            prototypes.Load(assets, "Prototypes").Should().Be(1);

            SpawnService spawner = provider.GetRequiredService<SpawnService>();
            var world = new World();
            Entity sprite = spawner.Spawn(world, "Broken");

            // A handle belongs to the run that made it, so it is state rather than data and is not part of the format: a
            // document that writes one is read without it, and content names an image with TexturePath instead.
            world.Get<SpriteComponent>(sprite).TexturePath.Should().BeNull();
            world.Get<SpriteComponent>(sprite).Texture.Id.Should().Be(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
