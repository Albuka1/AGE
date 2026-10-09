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

public sealed class PrototypeContentTests
{
    [Fact]
    public void PrototypeContent_WhatTheEngineShips_LoadsInheritsAndValidates()
    {
        using ServiceProvider provider = Create();
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        PrototypeManager prototypes = ReadAsEntity(provider);

        int count = prototypes.Load(assets, "Prototypes");

        count.Should().BeGreaterThan(0, "the engine ships content, and a game reads it the same way");

        Prototype goblin = prototypes.Get<Prototype>("Goblin");
        goblin.Parent.Should().Be("CreatureBase");
        goblin.Components.Select(component => component.Name).Should().Equal("Transform", "Collider", "Sprite", "SpriteAnimation");
        goblin.TryGet("Collider", out PrototypeComponent? collider).Should().BeTrue();
        collider!.Values.GetProperty("Size").GetProperty("X").GetInt32().Should().Be(24, "the goblin declares its own collider over the one of a creature");
        goblin.File.Should().Be("Prototypes/Entities/goblin.yml");

        Prototype creature = prototypes.Get<Prototype>("CreatureBase");
        creature.Components.Should().HaveCount(2, "a root prototype declares what its children share");
        creature.TryGet("Transform", out PrototypeComponent? transform).Should().BeTrue();
        transform!.Values.GetProperty("Scale").GetProperty("Y").GetInt32().Should().Be(1);

        Prototype sword = prototypes.Get<Prototype>("Sword");
        sword.Parent.Should().Be("ItemBase");
        sword.Components.Should().HaveCount(2);
    }

    [Fact]
    public void PrototypeManager_Load_ReadsEveryDocumentOfAFolderInOrderAndForgetsNothingElse()
    {
        string root = Path.Combine(Path.GetTempPath(), "age-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes", "group"));

        try
        {
            File.WriteAllText(Path.Combine(root, "Prototypes", "group", "base.yml"), "- id: Base\n  type: entity\n");
            File.WriteAllText(Path.Combine(root, "Prototypes", "one.yml"), "- id: One\n  type: entity\n  parent: Base\n");
            File.WriteAllText(Path.Combine(root, "Prototypes", "notes.txt"), "a note rather than a document\n");

            var assets = new NullAssetLoader();
            assets.Initialize(root);
            using ServiceProvider provider = Create();
            PrototypeManager prototypes = ReadAsEntity(provider);

            prototypes.Load(assets, "Prototypes").Should().Be(2, "a file that is not a document is left alone");
            prototypes.Ids.Should().Equal("Base", "One");
            prototypes.Ids.Should().HaveCount(2, "the folder below is read before the file beside it, on every machine");
            prototypes.Load(assets, "Nowhere").Should().Be(0, "a folder that is not there holds nothing rather than being an error");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Builds a container whose registrations hold the components of the engine.</summary>
    private static ServiceProvider Create() => new ServiceCollection()
        .AddAgeCore()
        .AddAgeContent()
        .AddAgePhysics()
        .AddAgeUI()
        .AddAgeRendering()
        .BuildServiceProvider();

    /// <summary>Takes the manager of a container and registers the kind that reads a prototype as the data itself.</summary>
    /// <remarks>The container stays alive while the manager is used: the manager is a singleton of it, not a copy of it.</remarks>
    private static PrototypeManager ReadAsEntity(ServiceProvider provider)
    {
        PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
        prototypes.Register<Prototype>("entity", prototype => prototype);
        return prototypes;
    }
}
