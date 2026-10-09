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
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        PrototypeManager prototypes = Create();

        int count = prototypes.Load(assets, "Prototypes");

        count.Should().BeGreaterThan(0, "the engine ships content, and a game reads it the same way");

        Prototype goblin = prototypes.Get<Prototype>("Goblin");
        goblin.Parent.Should().Be("CreatureBase");
        goblin.Components.Select(component => component.Name).Should().Equal("Transform", "Collider");
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
            PrototypeManager prototypes = Create();

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

    /// <summary>Builds a manager over the components of the engine, with one kind of prototype that holds the data itself.</summary>
    private static PrototypeManager Create()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeCore()
            .AddAgeContent()
            .AddAgePhysics()
            .AddAgeUI()
            .AddAgeRendering()
            .BuildServiceProvider();

        PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
        prototypes.Register<Prototype>("entity", prototype => prototype);
        return prototypes;
    }
}
