using System.Text.Json;
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

public sealed class PrototypeSceneTests
{
    [Fact]
    public void PrototypeScene_AHundredGoblins_AreWrittenAsOneReferenceEachAndLoadTheSame()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();

        List<Entity> spawned = [.. Enumerable.Range(0, 100).Select(_ => spawner.Spawn(world, "Goblin"))];

        string json = scenes.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement entities = document.RootElement.GetProperty("Entities");

            entities.GetArrayLength().Should().Be(100);
            entities.EnumerateArray().Should().OnlyContain(entity => entity.GetProperty("Prototype").GetString() == "Goblin");
            entities.EnumerateArray().Should().OnlyContain(entity => !entity.GetProperty("Components").EnumerateObject().Any(), "what a prototype declares is not written a hundred times");
        }

        var loaded = new World();
        scenes.Load(loaded, json);

        List<Entity> restored = [.. loaded.Enumerate()];

        restored.Should().HaveCount(100);
        restored.Select(entity => loaded.Get<ColliderComponent>(entity).Size).Should().OnlyContain(size => size == new Vector2(24f, 24f));
        restored.Select(entity => loaded.Get<TransformComponent>(entity).Scale).Should().OnlyContain(scale => scale == new Vector2(1f, 1f), "the components come back from the prototype");
        restored.Select(loaded.SceneIdOf).Should().Equal(spawned.Select(world.SceneIdOf), "the identifier of a scene survives a save and a load");
        loaded.PrototypeOf(restored[0]).Should().Be("Goblin", "the world remembers what an entity was made from, so a second save keeps the reference");
    }

    [Fact]
    public void PrototypeScene_AnOverride_IsWrittenAsTheDifferenceAndDoesNotFollowThePrototype()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();

        List<Entity> spawned = [.. Enumerable.Range(0, 3).Select(_ => spawner.Spawn(world, "Goblin"))];

        // One goblin of the three is bigger than what the prototype says, which is the difference that a scene keeps.
        world.GetRef<TransformComponent>(spawned[1]).Scale = new Vector2(2f, 2f);

        string json = scenes.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement entities = document.RootElement.GetProperty("Entities");

            entities[0].GetProperty("Components").EnumerateObject().Should().BeEmpty();
            entities[1].GetProperty("Components").EnumerateObject().Select(property => property.Name).Should().Equal("Transform");
            entities[1].GetProperty("Components").GetProperty("Transform").GetProperty("Scale").GetProperty("X").GetDouble().Should().Be(2d);
            entities[2].GetProperty("Components").EnumerateObject().Should().BeEmpty();
        }

        var loaded = new World();
        scenes.Load(loaded, json);

        List<Entity> restored = [.. loaded.Enumerate()];

        loaded.Get<TransformComponent>(restored[0]).Scale.Should().Be(new Vector2(1f, 1f), "the difference of one entity does not reach the others");
        loaded.Get<ColliderComponent>(restored[0]).Size.Should().Be(new Vector2(24f, 24f));
        loaded.Get<TransformComponent>(restored[1]).Scale.Should().Be(new Vector2(2f, 2f), "the difference is what the scene kept");
        loaded.Get<ColliderComponent>(restored[1]).Size.Should().Be(new Vector2(24f, 24f), "the difference does not push the rest of the prototype away");

        scenes.Save(loaded).Should().Be(json, "a scene that was loaded and written again is the scene it was");
    }

    [Fact]
    public void PrototypeScene_ADifferenceInOneField_KeepsOnlyThatFieldOfTheComponent()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();

        Entity goblin = spawner.Spawn(world, "Goblin");

        // The goblin keeps the position, the rotation and the size its prototype gave it and changes only its scale, so the scene
        // should carry the scale and nothing else of the transform: a component is written as the fields that differ, not whole.
        world.GetRef<TransformComponent>(goblin).Scale = new Vector2(3f, 3f);

        string json = scenes.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement transform = document.RootElement.GetProperty("Entities")[0].GetProperty("Components").GetProperty("Transform");

            transform.EnumerateObject().Select(property => property.Name).Should().Equal(new[] { "Scale" }, "only the field that differs is written");
            transform.GetProperty("Scale").GetProperty("X").GetDouble().Should().Be(3d);
        }

        // The fields the scene left out come back from the prototype rather than from a default of the structure.
        var loaded = new World();
        scenes.Load(loaded, json);

        Entity restored = loaded.Enumerate().Single();
        TransformComponent component = loaded.Get<TransformComponent>(restored);

        component.Scale.Should().Be(new Vector2(3f, 3f), "the field the scene wrote wins");
        component.Position.Should().Be(new Vector2(0f, 0f), "a field the scene left out keeps what the prototype gave it, which is the zero its document wrote");
        loaded.Get<ColliderComponent>(restored).Size.Should().Be(new Vector2(24f, 24f), "a component the scene did not name at all comes from the prototype");
    }

    [Fact]
    public void PrototypeScene_ASpriteOfLayers_IsWrittenAndReadBack()
    {
        using ServiceProvider provider = Create();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new SpriteComponent
        {
            Size = new Vector2(40f, 40f),
            Color = Color.White,
            Layers =
            [
                new SpriteLayer { Name = "base", Image = "Textures/Entities/thing.bmp" },
                new SpriteLayer { Name = "pulse", Image = "Textures/Entities/thing.bmp", Material = new Material { Id = "Pulse" } },
            ],
        });

        string json = scenes.Save(world);

        var loaded = new World();
        scenes.Load(loaded, json);

        SpriteComponent sprite = loaded.Get<SpriteComponent>(loaded.Enumerate().Single());
        sprite.Layers.Should().NotBeNull("the layers of a sprite are data of a document rather than state of a run");

        SpriteLayer[] layers = sprite.Layers!;
        layers.Select(layer => layer.Name).Should().Equal(new[] { "base", "pulse" }, "the order the document writes is what the layers are drawn in");
        layers[0].Image.Should().Be("Textures/Entities/thing.bmp");
        layers[1].Material.Id.Should().Be("Pulse");
    }

    [Fact]
    public void PrototypeScene_APrototypeThatTheContentDoesNotHold_IsRefusedAndLeavesTheWorldAsItWas()
    {
        using ServiceProvider provider = Create();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();
        Entity existing = world.CreateEntity();

        string json = """
        {
          "Version": 2,
          "Entities": [
            { "Id": 7, "Prototype": "Dragon", "Components": {} }
          ]
        }
        """;

        Action load = () => scenes.Load(world, json);

        load.Should().Throw<InvalidDataException>().WithMessage("*Dragon*");
        world.Enumerate().Should().ContainSingle().Which.Should().Be(existing, "a scene that cannot be made leaves the world as it was");
    }

    [Fact]
    public void PrototypeScene_APrototypeWithoutContent_IsRefused()
    {
        using ServiceProvider provider = Create();
        var scenes = new SceneSerializer(provider.GetRequiredService<ComponentRegistry>());
        var world = new World();

        string json = """
        {
          "Entities": [
            { "Id": 1, "Prototype": "Goblin", "Components": {} }
          ]
        }
        """;

        Action load = () => scenes.Load(world, json);

        load.Should().Throw<InvalidDataException>().WithMessage("*no content*");
        world.Enumerate().Should().BeEmpty("an entity of a prototype is not made half way");
    }

    [Fact]
    public void PrototypeScene_AnEmptyPrototypeName_IsRefused()
    {
        using ServiceProvider provider = Create();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();

        string json = """
        {
          "Entities": [
            { "Id": 1, "Prototype": "   ", "Components": {} }
          ]
        }
        """;

        Action load = () => scenes.Load(world, json);

        load.Should().Throw<InvalidDataException>().WithMessage("*empty prototype*");
    }

    [Fact]
    public void World_PrototypeOf_IsForgottenWhenTheSlotIsHandedOutAgain()
    {
        var world = new World();
        Entity goblin = world.CreateEntity();

        world.PrototypeOf(goblin).Should().BeNull("an entity that no spawn made was not made from a prototype");
        world.AssignPrototype(goblin, "Goblin");
        world.PrototypeOf(goblin).Should().Be("Goblin");

        world.DestroyEntity(goblin);
        world.PrototypeOf(goblin).Should().BeNull("a destroyed entity is not made from anything");

        Action assign = () => world.AssignPrototype(goblin, "Goblin");
        assign.Should().Throw<InvalidOperationException>();

        Entity next = world.CreateEntity();

        next.Id.Should().Be(goblin.Id, "the slot of a destroyed entity is handed out again");
        next.Generation.Should().NotBe(goblin.Generation, "the slot comes back in a new generation, so the old identifier stays stale");
        world.PrototypeOf(next).Should().BeNull("the entity that takes the slot is not the one that was made from a prototype");
    }

    [Fact]
    public void PrototypeScene_AComponentThatDiffersFromThePrototype_IsWrittenAndComesBack()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();

        List<Entity> spawned = [.. Enumerable.Range(0, 2).Select(_ => spawner.Spawn(world, "Goblin"))];

        // One goblin of the two draws something other than what its kind draws: the scene writes that one component and
        // leaves the other goblin as the prototype describes it.
        world.Set(spawned[0], new SpriteComponent { TexturePath = "Textures/Tiles/tiles.bmp", Size = new Vector2(32f, 32f), Color = Color.Red });

        string json = scenes.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement entities = document.RootElement.GetProperty("Entities");

            entities[0].GetProperty("Prototype").GetString().Should().Be("Goblin");
            entities[0].GetProperty("Components").EnumerateObject().Select(property => property.Name).Should().Equal("Sprite");
            entities[0].GetProperty("Components").GetProperty("Sprite").GetProperty("TexturePath").GetString().Should().Be("Textures/Tiles/tiles.bmp");
            entities[1].GetProperty("Components").EnumerateObject().Should().BeEmpty("the second goblin is nothing but its prototype");
        }

        var loaded = new World();
        scenes.Load(loaded, json);

        List<Entity> restored = [.. loaded.Enumerate()];

        loaded.Get<SpriteComponent>(restored[0]).TexturePath.Should().Be("Textures/Tiles/tiles.bmp");
        loaded.Get<SpriteComponent>(restored[0]).Color.Should().Be(Color.Red);
        loaded.Get<SpriteComponent>(restored[1]).SheetPath.Should().Be("Textures/Entities/goblin.yml", "the second goblin draws what its prototype says");
        loaded.Get<SpriteComponent>(restored[1]).TexturePath.Should().BeNull("the difference of one entity does not follow the other");
        loaded.Get<SpriteAnimationComponent>(restored[1]).State.Should().Be("walk", "and what its prototype says about playing comes with it");
    }

    [Fact]
    public void PrototypeScene_APrototypeThatThisBuildDoesNotHold_IsNotNamedAndTheEntityIsWrittenInFull()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        var world = new World();
        Entity goblin = spawner.Spawn(world, "Goblin");

        // A build whose content no longer declares the kind of an entity — a prototype that was renamed, or content of a mod
        // that was taken out — writes the entity with every component it holds rather than a reference that nothing can
        // resolve: a scene that this build writes has to be one that this build reads. The source below is a real one over a
        // manager that read no content, which is what a build without that kind has.
        ComponentRegistry components = provider.GetRequiredService<ComponentRegistry>();
        var withoutTheKind = new SceneSerializer(components, prototypes: new SpawnService(new PrototypeManager(components), components));
        string json = withoutTheKind.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement entity = document.RootElement.GetProperty("Entities")[0];

            entity.GetProperty("Prototype").ValueKind.Should().Be(JsonValueKind.Null, "a prototype that the content does not hold is not written");
            entity.GetProperty("Components").EnumerateObject().Select(property => property.Name)
                .Should().Contain("Sprite").And.Contain("Transform", "the entity is written in full instead");
        }

        var loaded = new World();
        withoutTheKind.Load(loaded, json);

        Entity restored = loaded.Enumerate().Single();

        loaded.Get<SpriteComponent>(restored).SheetPath.Should().Be("Textures/Entities/goblin.yml");
        loaded.Get<ColliderComponent>(restored).Size.Should().Be(new Vector2(24f, 24f), "what the prototype declared comes back with the entity itself");
        loaded.PrototypeOf(restored).Should().BeNull("and nothing names the kind it came from, so the file is read by a build without that kind");
    }

    [Fact]
    public void PrototypeScene_APrototypeWhoseComponentIsNotRegistered_IsRefusedAndLeavesTheWorldAsItWas()
    {
        using ServiceProvider provider = Create();
        var world = new World();
        Entity existing = world.CreateEntity();
        var scenes = new SceneSerializer(provider.GetRequiredService<ComponentRegistry>(), prototypes: new UnregisteredComponent());

        Action load = () => scenes.Load(world, Scene("Goblin"));

        load.Should().Throw<InvalidDataException>().WithMessage("*'Goblin'*'Nope'*");
        world.Enumerate().Should().ContainSingle().Which.Should().Be(existing, "content that cannot be made is refused before the world is touched");
    }

    [Fact]
    public void PrototypeScene_APrototypeWhoseValuesCannotBeRead_IsRefusedAndLeavesTheWorldAsItWas()
    {
        using ServiceProvider provider = Create();
        var world = new World();
        Entity existing = world.CreateEntity();
        var scenes = new SceneSerializer(provider.GetRequiredService<ComponentRegistry>(), prototypes: new UnreadableComponent());

        Action load = () => scenes.Load(world, Scene("Goblin"));

        load.Should().Throw<InvalidDataException>().WithMessage("*'Transform'*'Goblin'*cannot be read*");
        world.Enumerate().Should().ContainSingle().Which.Should().Be(existing, "a value that the contract refuses is not a world that was changed half way");
    }

    [Fact]
    public void PrototypeScene_APrototypeWhoseComponentHasAFieldTheFormatDoesNotCarry_IsRefusedAndLeavesTheWorldAsItWas()
    {
        using ServiceProvider provider = Create();
        var world = new World();
        Entity existing = world.CreateEntity();
        var scenes = new SceneSerializer(provider.GetRequiredService<ComponentRegistry>(), prototypes: new UnknownField());

        Action load = () => scenes.Load(world, Scene("Goblin"));

        InvalidDataException refused = load.Should().Throw<InvalidDataException>().Which;

        refused.Message.Should().Contain("'Collider'").And.Contain("'Goblin'").And.Contain("cannot be read");
        refused.InnerException.Should().BeOfType<JsonException>("the refusal comes from the format of the component rather than from a value that a serializer cannot map")
            .Which.Message.Should().Contain("is not one that the component 'Collider' carries");
        world.Enumerate().Should().ContainSingle().Which.Should().Be(existing, "a field that the format does not carry is refused before the world is touched");
    }

    [Fact]
    public void PrototypeScene_AnEntityThatLostAComponent_IsWrittenInFullAndKeepsWhatItStillHolds()
    {
        using ServiceProvider provider = Create();
        SpawnService spawner = provider.GetRequiredService<SpawnService>();
        ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
        var world = new World();
        Entity goblin = spawner.Spawn(world, "Goblin");

        // A goblin that lost a component of its kind is written in full, because a scene has no way to say "without the
        // component of the prototype": every component it still holds goes into the file, including the ones that its
        // prototype declares as well.
        world.Remove<SpriteComponent>(goblin);

        string json = scenes.Save(world);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement entity = document.RootElement.GetProperty("Entities")[0];
            List<string> written = [.. entity.GetProperty("Components").EnumerateObject().Select(property => property.Name)];

            entity.GetProperty("Prototype").ValueKind.Should().Be(JsonValueKind.Null);
            written.Should().Contain("Transform").And.Contain("Collider").And.Contain("SpriteAnimation").And.NotContain("Sprite");
        }

        var loaded = new World();
        scenes.Load(loaded, json);

        Entity restored = loaded.Enumerate().Single();

        loaded.Has<SpriteComponent>(restored).Should().BeFalse("the component that the entity lost stays lost");
        loaded.Get<ColliderComponent>(restored).Size.Should().Be(new Vector2(24f, 24f), "what the prototype declared comes back with the entity itself");
        loaded.Get<SpriteAnimationComponent>(restored).State.Should().Be("walk");
    }

    /// <summary>Returns a scene of one entity that names a prototype and carries no component of its own.</summary>
    private static string Scene(string prototypeId) => $$"""
        {
          "Version": 2,
          "Entities": [
            { "Id": 7, "Prototype": "{{prototypeId}}", "Components": {} }
          ]
        }
        """;

    /// <summary>A source whose prototype declares a component that nothing registers, which is content a build cannot read.</summary>
    private sealed class UnregisteredComponent : IPrototypeSource
    {
        public Entity Spawn(World world, string prototypeId) => throw new NotSupportedException("a scene with such a prototype never reaches the spawn");

        public IReadOnlyDictionary<string, JsonElement>? ComponentsOf(string prototypeId) =>
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["Nope"] = JsonDocument.Parse("{}").RootElement.Clone() };
    }

    /// <summary>A source whose prototype declares values that the contract of a component refuses.</summary>
    private sealed class UnreadableComponent : IPrototypeSource
    {
        public Entity Spawn(World world, string prototypeId) => throw new NotSupportedException("a scene with such a prototype never reaches the spawn");

        public IReadOnlyDictionary<string, JsonElement>? ComponentsOf(string prototypeId) =>
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["Transform"] = JsonDocument.Parse("""{ "Position": "nowhere" }""").RootElement.Clone() };
    }

    /// <summary>A source whose prototype declares a component with a field that the format of the component does not carry.</summary>
    private sealed class UnknownField : IPrototypeSource
    {
        public Entity Spawn(World world, string prototypeId) => throw new NotSupportedException("a scene with such a prototype never reaches the spawn");

        public IReadOnlyDictionary<string, JsonElement>? ComponentsOf(string prototypeId) =>
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["Collider"] = JsonDocument.Parse("""{ "Nope": 1 }""").RootElement.Clone() };
    }

    /// <summary>Builds a provider whose content is the one the engine ships, read as entities.</summary>
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
        prototypes.Register(MaterialPrototype.Kind, MaterialPrototype.Read);

        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        prototypes.Load(assets, "Prototypes");

        return provider;
    }
}
