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

        serializer.Load(world, json);

        world.IsAlive(existing).Should().BeTrue();
        world.Enumerate<TransformComponent>().Should().HaveCount(2);
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

    private static SceneSerializer CreateSerializer()
    {
        using ServiceProvider provider = CreateProvider();
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
