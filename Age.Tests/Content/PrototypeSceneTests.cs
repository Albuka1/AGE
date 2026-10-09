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

        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        prototypes.Load(assets, "Prototypes");

        return provider;
    }
}
