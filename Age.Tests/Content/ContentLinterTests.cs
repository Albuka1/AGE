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
        var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));

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
    public void ContentLinter_AFieldThatIsAProperty_IsNotReported()
    {
        // A component whose data are properties is read by the same contract as one whose data are fields: the registrations
        // carry the name of either, so a document that writes a property is read rather than dropped, and the linter has to
        // look where the document writes.
        LintReport report = Lint("- type: entity\n  id: Timed\n  components:\n    - type: Timer\n      Duration: 2\n      Repeat: true\n");

        report.IsClean.Should().BeTrue("the document writes members that the component really carries");
        report.Count.Should().Be(1);
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

    [Fact]
    public void ContentLinter_ASheetOfABuild_IsCheckedAgainstItsImage()
    {
        LintReport report = LintSheet(Sheet);

        report.IsClean.Should().BeTrue("the document and the picture beside it agree");
        report.Count.Should().Be(1);
    }

    [Fact]
    public void ContentLinter_ASheetWhoseGridIsNotItsImage_IsRefused()
    {
        LintReport report = LintSheet(Sheet.Replace("columns: 4", "columns: 3", StringComparison.Ordinal));

        report.Problems.Should().ContainSingle().Which.Message
            .Should().Contain("3 by 2 cells of 16 by 16 pixels, which is 48 by 32")
            .And.Contain("64 by 32", "which is what the image of the sheet really is");
    }

    [Fact]
    public void ContentLinter_ASheetThatDoesNotSayWhereItsArtComesFrom_IsRefused()
    {
        string document = Sheet
            .Replace("license: MIT\n", string.Empty, StringComparison.Ordinal)
            .Replace("copyright: AGE, drawn for this repository\n", string.Empty, StringComparison.Ordinal);

        LintReport report = LintSheet(document);

        report.Problems.Should().HaveCount(2, "a sheet says which licence it comes with and who its art belongs to");
        report.Problems.Should().Contain(problem => problem.Message.Contains("licence", StringComparison.Ordinal));
        report.Problems.Should().Contain(problem => problem.Message.Contains("belongs to", StringComparison.Ordinal));
    }

    [Fact]
    public void ContentLinter_ASheetOfAnotherVersion_IsRefused()
    {
        LintReport report = LintSheet(Sheet.Replace("version: 1", "version: 2", StringComparison.Ordinal));

        report.Count.Should().Be(0, "a document of a version this build does not read stops the reading");
        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("version 2").And.Contain("reads version 1");
    }

    [Fact]
    public void ContentLinter_TheSheetsOfTheEngine_AreSound()
    {
        using ServiceProvider provider = Create();
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));

        LintReport report = linter.LintSheets("Textures");

        report.IsClean.Should().BeTrue("the sheet of the engine is what a build ships");
        report.Count.Should().Be(1);
    }

    /// <summary>A document of a sheet, written the way the sheets of the engine are.</summary>
    private const string Sheet =
        "version: 1\n"
        + "license: MIT\n"
        + "copyright: AGE, drawn for this repository\n"
        + "image: Textures/Entities/goblin.tga\n"
        + "cell:\n"
        + "  X: 16\n"
        + "  Y: 16\n"
        + "columns: 4\n"
        + "rows: 2\n"
        + "states:\n"
        + "  idle:\n"
        + "    row: 0\n"
        + "    frames: 2\n";

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

            var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));

            return linter.Lint("Prototypes");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Lints the sheet of a folder that holds one document and the image it names.</summary>
    private static LintReport LintSheet(string document)
    {
        string root = Path.Combine(Path.GetTempPath(), "age-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Textures", "Entities"));

        try
        {
            // The picture of the shipped sheet stands in for the art of a game, so the document is checked against an image
            // that the loader of images really reads.
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Resources", "Textures", "Entities", "goblin.tga"),
                Path.Combine(root, "Textures", "Entities", "goblin.tga"));
            File.WriteAllText(Path.Combine(root, "Textures", "Entities", "goblin.yml"), document);

            using ServiceProvider provider = Create();
            var assets = new NullAssetLoader();
            assets.Initialize(root);
            var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));

            return linter.LintSheets("Textures");
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
