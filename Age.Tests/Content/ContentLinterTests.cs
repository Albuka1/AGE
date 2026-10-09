using Age.Assets;
using Age.Audio;
using Age.Content;
using Age.Content.Lint;
using Age.Content.Prototypes;
using Age.Core;
using Age.Input;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class ContentLinterTests
{
    [Fact]
    public void ContentLinter_WhatTheEngineShips_IsSound()
    {
        using ServiceProvider provider = Create();
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets);

        LintReport report = linter.Lint("Prototypes");

        report.IsClean.Should().BeTrue("the content of the engine is what a build ships");
        report.Count.Should().Be(4);
    }

    [Fact]
    public void ContentLinter_AFieldThatTheFormatDoesNotCarry_IsRefusedByTheReader()
    {
        // A handle of a texture is state of a run rather than data of content, so the format does not carry it: the reading
        // of the document refuses it, and the linter reports what the reader said, with the file and the line.
        LintReport report = Lint("- type: entity\n  id: Broken\n  components:\n    - type: Sprite\n      Texture: 7\n");

        report.Count.Should().Be(0, "a document that the reader refuses stops the reading");
        LintProblem problem = report.Problems.Should().ContainSingle().Subject;

        problem.File.Should().EndWith("thing.yml");
        problem.Line.Should().BeGreaterThan(0, "a problem says where a person has to open a file");
        problem.Message.Should().Contain("Texture").And.Contain("carries");
        problem.ToString().Should().Contain("thing.yml(", "the text of a problem is written the way a compiler writes one");
    }

    [Fact]
    public void ContentLinter_AFieldThatTheComponentDoesNotHave_IsReportedByTheReader()
    {
        LintReport report = Lint("- type: entity\n  id: Broken\n  components:\n    - type: Sprite\n      Nope: 1\n");

        report.Count.Should().Be(0, "a document that the reader refuses stops the reading");
        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("Nope").And.Contain("thing.yml");
    }

    [Fact]
    public void ContentLinter_ResourcesThatAreNotThere_AreReported()
    {
        LintReport report = Lint("- type: entity\n  id: Broken\n  components:\n    - type: Sprite\n      TexturePath: Textures/Nowhere/gone.png\n");

        report.Count.Should().Be(1);
        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("Textures/Nowhere/gone.png");
    }

    [Fact]
    public void ContentLinter_ResourcesThatAreThere_AreNotReported()
    {
        LintReport report = Lint(
            "- type: entity\n  id: Fine\n  components:\n    - type: Sprite\n      TexturePath: Textures/Entities/thing.bmp\n",
            "Textures/Entities/thing.bmp");

        report.IsClean.Should().BeTrue("the file the content names is one the build ships");
        report.Count.Should().Be(1);
    }

    /// <summary>Lints one document that is written into a game root of its own, together with the files it may name.</summary>
    private static LintReport Lint(string document, params string[] files)
    {
        string root = Path.Combine(Path.GetTempPath(), "age-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes"));

        try
        {
            File.WriteAllText(Path.Combine(root, "Prototypes", "thing.yml"), document);

            foreach (string file in files)
            {
                string path = Path.Combine(root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            using ServiceProvider provider = Create();
            var assets = new NullAssetLoader();
            assets.Initialize(root);

            var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets);

            return linter.Lint("Prototypes");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Takes the content of a container and registers the kind that reads a document as an entity.</summary>
    private static PrototypeManager ReadContent(ServiceProvider provider)
    {
        PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
        prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);
        return prototypes;
    }

    /// <summary>Builds a container that holds the components of every assembly of the engine.</summary>
    private static ServiceProvider Create() => new ServiceCollection()
        .AddAgeCore()
        .AddAgeContent()
        .AddAgeAssets()
        .AddAgeInput()
        .AddAgeAudio()
        .AddAgePhysics()
        .AddAgeUI()
        .AddAgeRendering()
        .BuildServiceProvider();
}
