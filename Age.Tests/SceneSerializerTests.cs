using System.Text.Json;
using System.Text.Json.Serialization;
using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class SceneSerializerTests
{
    [Fact]
    public void SceneSerializer_SaveAndLoad_RoundTripsTheComponents()
    {
        SceneSerializer serializer = CreateSerializer();
        var source = new World();
        Entity sprite = source.CreateEntity();
        source.Set(sprite, new TransformComponent { Position = new Vector2(400f, 300f), Scale = new Vector2(2f, 3f), Rotation = 0.5f });
        source.Set(sprite, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Green, ZOrder = 2 });
        Entity panel = source.CreateEntity();
        source.Set(panel, new RectTransformComponent { Position = new Vector2(100f, 100f), Size = new Vector2(200f, 50f), ZOrder = 1, Visible = true });
        source.Set(panel, new ButtonComponent { BaseColor = Color.Blue, Interactable = true });
        source.Set(panel, new TextLabelComponent { Text = "Hello AGE", Color = Color.White });

        string json = serializer.Save(source);
        var loaded = new World();
        serializer.Load(loaded, json);

        List<Entity> sprites = [.. loaded.Enumerate<TransformComponent>()];
        sprites.Should().HaveCount(1);
        loaded.Get<TransformComponent>(sprites[0]).Position.Should().Be(new Vector2(400f, 300f));
        loaded.Get<TransformComponent>(sprites[0]).Scale.Should().Be(new Vector2(2f, 3f));
        loaded.Get<TransformComponent>(sprites[0]).Rotation.Should().Be(0.5f);
        loaded.Get<SpriteComponent>(sprites[0]).Size.Should().Be(new Vector2(64f, 64f));
        loaded.Get<SpriteComponent>(sprites[0]).Color.Should().Be(Color.Green);
        loaded.Get<SpriteComponent>(sprites[0]).ZOrder.Should().Be(2);

        List<Entity> panels = [.. loaded.Enumerate<ButtonComponent>()];
        panels.Should().HaveCount(1);
        loaded.Get<RectTransformComponent>(panels[0]).Size.Should().Be(new Vector2(200f, 50f));
        loaded.Get<RectTransformComponent>(panels[0]).Visible.Should().BeTrue();
        loaded.Get<ButtonComponent>(panels[0]).BaseColor.Should().Be(Color.Blue);
        loaded.Get<ButtonComponent>(panels[0]).Interactable.Should().BeTrue();
        loaded.Get<TextLabelComponent>(panels[0]).Text.Should().Be("Hello AGE");
        loaded.Get<TextLabelComponent>(panels[0]).Color.Should().Be(Color.White);
    }

    [Fact]
    public void SceneSerializer_Save_WritesTheComponentsUnderTheirRegisteredNames()
    {
        SceneSerializer serializer = CreateSerializer();
        var world = new World();
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = new Vector2(7f, 8f), Scale = new Vector2(1f, 1f) });

        string json = serializer.Save(world);

        json.Should().Contain("\"Transform\"").And.Contain("\"Position\"");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement component = document.RootElement.GetProperty("Entities")[0].GetProperty("Components").GetProperty("Transform");
        component.GetProperty("Position").GetProperty("X").GetSingle().Should().Be(7f);
        component.GetProperty("Position").GetProperty("Y").GetSingle().Should().Be(8f);
    }

    [Fact]
    public void SceneSerializer_Load_KeepsTheEntitiesTheWorldAlreadyHad()
    {
        SceneSerializer serializer = CreateSerializer();
        var source = new World();
        Entity saved = source.CreateEntity();
        source.Set(saved, new TransformComponent { Position = new Vector2(1f, 2f) });
        string json = serializer.Save(source);

        var world = new World();
        Entity existing = world.CreateEntity();
        world.Set(existing, new TransformComponent { Position = new Vector2(9f, 9f) });
        int identifier = world.SceneIdOf(existing);

        serializer.Load(world, json);

        world.IsAlive(existing).Should().BeTrue();
        world.Enumerate<TransformComponent>().Should().HaveCount(2);
        world.SceneIdOf(existing).Should().NotBe(identifier, "the scene brought an entity that uses that identifier");
        world.TryEntityOf(identifier, out Entity loaded).Should().BeTrue();
        loaded.Should().NotBe(existing, "the identifier of the scene belongs to the entity it brought");
    }

    [Fact]
    public void SceneSerializer_LoadAComponentThatIsNotRegistered_ThrowsInvalidDataException()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """{ "Entities": [ { "Components": { "Mystery": { "Value": 1 } } } ] }""";

        Action act = () => serializer.Load(new World(), Json);

        act.Should().Throw<InvalidDataException>().WithMessage("*Mystery*");
    }

    [Fact]
    public void SceneSerializer_LoadTextThatIsNotJson_ThrowsInvalidDataException()
    {
        SceneSerializer serializer = CreateSerializer();

        Action act = () => serializer.Load(new World(), "this is not json");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ComponentRegistry_RegisteringTheNameOrTheTypeTwice_ThrowsInvalidOperationException()
    {
        ComponentRegistry registry = CreateRegistry();

        Action sameName = () => registry.Register("Transform", TestJsonContext.Default.TransformComponent);
        Action sameType = () => registry.Register("Other", TestJsonContext.Default.TransformComponent);

        sameName.Should().Throw<InvalidOperationException>();
        sameType.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ComponentRegistry_CollectsTheComponentsOfEveryAssemblyThatRegisters()
    {
        ComponentRegistry registry = CreateRegistry();

        registry.Names.Should().Contain(["Transform", "Sprite", "RectTransform", "Button", "TextLabel"]);
        registry.TryGetType("Sprite", out Type? sprite).Should().BeTrue();
        sprite.Should().Be(typeof(SpriteComponent));
        registry.TryGetType("Mystery", out _).Should().BeFalse();
    }

    [Fact]
    public void SceneSerializer_LoadASceneThatFailsHalfway_LeavesTheWorldUnchanged()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """
            {
              "Entities": [
                { "Components": { "Transform": { "Position": { "X": 1, "Y": 1 }, "Scale": { "X": 1, "Y": 1 } } } },
                { "Components": { "Mystery": { "Value": 1 } } }
              ]
            }
            """;
        var world = new World();

        Action act = () => serializer.Load(world, Json);

        act.Should().Throw<InvalidDataException>();
        world.Enumerate().Should().BeEmpty();
    }

    [Fact]
    public void SceneSerializer_LoadAComponentThatCannotBeRead_LeavesTheWorldUnchanged()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """{ "Entities": [ { "Components": { "Transform": { "Position": 5 } } } ] }""";
        var world = new World();

        Action act = () => serializer.Load(world, Json);

        act.Should().Throw<InvalidDataException>();
        world.Enumerate().Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "Entities": null }""")]
    [InlineData("""{ "Entities": [ null ] }""")]
    [InlineData("""{ "Entities": [ { "Components": null } ] }""")]
    public void SceneSerializer_LoadATextWithoutUsableEntities_ThrowsInvalidDataException(string json)
    {
        SceneSerializer serializer = CreateSerializer();
        var world = new World();

        Action act = () => serializer.Load(world, json);

        act.Should().Throw<InvalidDataException>();
        world.Enumerate().Should().BeEmpty();
    }

    [Fact]
    public void SceneSerializer_Save_KeepsAnEntityThatHasNoRegisteredComponents()
    {
        SceneSerializer serializer = CreateSerializer();
        var world = new World();
        world.CreateEntity();
        world.CreateEntity();

        string json = serializer.Save(world);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement entities = document.RootElement.GetProperty("Entities");
        entities.GetArrayLength().Should().Be(2);
        entities[0].GetProperty("Components").EnumerateObject().Should().BeEmpty();
        entities[1].GetProperty("Components").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void SceneSerializer_Save_WritesTheVersionAndTheIdentifiersOfTheScene()
    {
        SceneSerializer serializer = CreateSerializer();
        var world = new World();
        Entity first = world.CreateEntity();
        Entity second = world.CreateEntity();

        using JsonDocument document = JsonDocument.Parse(serializer.Save(world));

        document.RootElement.GetProperty("Version").GetInt32().Should().Be(SceneData.CurrentVersion);
        JsonElement entities = document.RootElement.GetProperty("Entities");
        entities[0].GetProperty("Id").GetInt32().Should().Be(world.SceneIdOf(first));
        entities[1].GetProperty("Id").GetInt32().Should().Be(world.SceneIdOf(second));
        world.SceneIdOf(first).Should().NotBe(world.SceneIdOf(second));
    }

    [Fact]
    public void SceneSerializer_SaveAndLoad_KeepsTheIdentifiersOfTheScene()
    {
        SceneSerializer serializer = CreateSerializer();
        var source = new World();
        Entity first = source.CreateEntity();
        Entity second = source.CreateEntity();
        string json = serializer.Save(source);

        var loaded = new World();
        serializer.Load(loaded, json);

        List<Entity> entities = [.. loaded.Enumerate()];
        entities.Should().HaveCount(2);
        loaded.SceneIdOf(entities[0]).Should().Be(source.SceneIdOf(first));
        loaded.SceneIdOf(entities[1]).Should().Be(source.SceneIdOf(second));
        loaded.TryEntityOf(loaded.SceneIdOf(entities[1]), out Entity resolved).Should().BeTrue();
        resolved.Should().Be(entities[1]);
    }

    [Fact]
    public void SceneSerializer_SaveAndLoad_KeepsAReferenceToAnotherEntity()
    {
        SceneSerializer serializer = CreateSerializerWithTarget();
        var source = new World();
        Entity owner = source.CreateEntity();
        Entity target = source.CreateEntity();
        source.Set(owner, new TargetComponent { Target = source.Reference(target) });
        int targetIdentifier = source.SceneIdOf(target);

        string json = serializer.Save(source);

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement reference = document.RootElement.GetProperty("Entities")[0].GetProperty("Components").GetProperty("Target").GetProperty("Target");
            reference.ValueKind.Should().Be(JsonValueKind.Number, "a reference is written as the number of the entity in its scene");
            reference.GetInt32().Should().Be(targetIdentifier);
        }

        var loaded = new World();
        serializer.Load(loaded, json);

        Entity loadedOwner = loaded.Enumerate<TargetComponent>().Single();
        Entity loadedTarget = loaded.Resolve(loaded.Get<TargetComponent>(loadedOwner).Target);

        loadedTarget.Should().NotBe(default(Entity), "the reference reads back as an entity");
        loaded.IsAlive(loadedTarget).Should().BeTrue();
        loaded.SceneIdOf(loadedTarget).Should().Be(targetIdentifier);
    }

    [Fact]
    public void SceneSerializer_LoadAReferenceToAnEntityTheSceneDoesNotHold_ResolvesToNothing()
    {
        SceneSerializer serializer = CreateSerializerWithTarget();
        const string Json = """
            {
              "Version": 1,
              "Entities": [ { "Id": 1, "Components": { "Target": { "Target": 7 } } } ]
            }
            """;
        var world = new World();

        serializer.Load(world, Json);

        Entity owner = world.Enumerate<TargetComponent>().Single();
        EntityRef reference = world.Get<TargetComponent>(owner).Target;

        reference.HasValue.Should().BeTrue("the text names an entity");
        reference.IsAlive(world).Should().BeFalse("the scene holds no entity with that identifier");
        world.Resolve(reference).Should().Be(default(Entity));
    }

    [Fact]
    public void SceneSerializer_LoadASceneOfANewerVersion_ThrowsInvalidDataException()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """{ "Version": 99, "Entities": [] }""";

        Action act = () => serializer.Load(new World(), Json);

        act.Should().Throw<InvalidDataException>().WithMessage("*version 99*");
    }

    [Fact]
    public void SceneSerializer_LoadASceneWithTwoEntitiesOfTheSameIdentifier_ThrowsInvalidDataException()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """
            {
              "Entities": [
                { "Id": 4, "Components": {} },
                { "Id": 4, "Components": {} }
              ]
            }
            """;
        var world = new World();

        Action act = () => serializer.Load(world, Json);

        act.Should().Throw<InvalidDataException>().WithMessage("*identifier 4*");
        world.Enumerate().Should().BeEmpty("a scene that turns out to be broken leaves the world as it was");
    }

    [Fact]
    public void SceneSerializer_LoadASceneOfTwoEntitiesWithoutIdentifiers_AssignsThem()
    {
        SceneSerializer serializer = CreateSerializer();
        const string Json = """{ "Entities": [ { "Components": {} }, { "Components": {} } ] }""";
        var world = new World();

        serializer.Load(world, Json);

        List<Entity> entities = [.. world.Enumerate()];
        entities.Should().HaveCount(2);
        world.SceneIdOf(entities[0]).Should().NotBe(0);
        world.SceneIdOf(entities[1]).Should().NotBe(0);
        world.SceneIdOf(entities[0]).Should().NotBe(world.SceneIdOf(entities[1]));
    }

    private static SceneSerializer CreateSerializer()
    {
        using ServiceProvider provider = CreateProvider();
        return new SceneSerializer(provider.GetRequiredService<ComponentRegistry>());
    }

    private static SceneSerializer CreateSerializerWithTarget()
    {
        var services = new ServiceCollection();
        services.AddAgeCore();
        services.AddAgeRendering();
        services.AddAgeUI();
        services.AddSingleton<IComponentRegistrations, TargetRegistrations>();
        using ServiceProvider provider = services.BuildServiceProvider();
        return new SceneSerializer(provider.GetRequiredService<ComponentRegistry>());
    }

    private static ComponentRegistry CreateRegistry()
    {
        using ServiceProvider provider = CreateProvider();
        return provider.GetRequiredService<ComponentRegistry>();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddAgeCore();
        services.AddAgeRendering();
        services.AddAgeUI();
        return services.BuildServiceProvider();
    }
}

/// <summary>A source generated context of a game, which is what a game writes for the components it adds.</summary>
[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(TransformComponent))]
internal sealed partial class TestJsonContext : JsonSerializerContext
{
}

/// <summary>A component of a game that refers to another entity, which a scene keeps by identifier.</summary>
internal struct TargetComponent : IComponent
{
    public EntityRef Target;
}

/// <summary>The registration of the game component, which is what a game writes for the components it adds.</summary>
internal sealed class TargetRegistrations : IComponentRegistrations
{
    /// <inheritdoc />
    public void Register(ComponentRegistry registry) => registry.Register("Target", GameJsonContext.Default.TargetComponent);
}

/// <summary>A second source generated context of a game, for the component that holds a reference.</summary>
[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(TargetComponent))]
internal sealed partial class GameJsonContext : JsonSerializerContext
{
}
