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

    [Fact]
    public void ContentLinter_AStateThatTheSheetDoesNotDeclare_IsReported()
    {
        LintReport report = LintWith(
            "- type: entity\n  id: Broken\n  components:\n    - type: Sprite\n      SheetPath: Textures/Entities/goblin.yml\n      State: flying\n",
            ("Textures/Entities/goblin.yml", Sheet));

        report.Problems.Should().ContainSingle().Which.Message
            .Should().Contain("'flying'")
            .And.Contain("Textures/Entities/goblin.yml")
            .And.Contain("idle", "a message says which states the sheet does declare");
    }

    [Fact]
    public void ContentLinter_AStateThatTheSheetDeclares_IsNotReported()
    {
        LintReport report = LintWith(
            "- type: entity\n  id: Fine\n  components:\n    - type: Sprite\n      SheetPath: Textures/Entities/goblin.yml\n      State: idle\n",
            ("Textures/Entities/goblin.yml", Sheet));

        report.IsClean.Should().BeTrue("the state is one the document of the sheet declares");
        report.Count.Should().Be(1);
    }

    [Fact]
    public void ContentLinter_AStateThatTheAnimationNames_IsCheckedAgainstTheSheetOfTheSprite()
    {
        // The animation of an entity plays a state of the sheet that the sprite of the same entity names, so a state it asks
        // for is one the document has to declare.
        LintReport report = LintWith(
            "- type: entity\n  id: Broken\n  components:\n    - type: Sprite\n      SheetPath: Textures/Entities/goblin.yml\n    - type: SpriteAnimation\n      State: flying\n",
            ("Textures/Entities/goblin.yml", Sheet));

        report.Problems.Should().ContainSingle().Which.Message
            .Should().Contain("'flying'")
            .And.Contain("SpriteAnimation")
            .And.Contain("idle");
    }

    [Fact]
    public void ContentLinter_ASheetWhoseImageIsNotThere_IsReported()
    {
        LintReport report = LintSheets(root =>
        {
            string folder = Path.Combine(root, "Textures", "Entities");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "goblin.yml"), Sheet);
        });

        report.Count.Should().Be(1, "the document is a sheet of the build whether or not its image is there");
        report.Problems.Should().ContainSingle().Which.Message
            .Should().Contain("Textures/Entities/goblin.tga")
            .And.Contain("cannot be read");
    }

    [Fact]
    public void ContentLinter_ASheetsFolderThatIsNotThere_IsReportedWithItsName()
    {
        // A build that is pointed at a folder which is not there hears about it: the tool names the folder rather than
        // reading nothing in silence, which is what a typo in the arguments looks like.
        LintReport report = LintSheets(_ => { });

        report.Count.Should().Be(0);
        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("Textures").And.Contain("does not exist");
    }

    /// <summary>Lints one document that is written into a game root of its own, together with files that hold content.</summary>
    private static LintReport LintWith(string document, params (string Path, string Content)[] files)
    {
        string root = Path.Combine(Path.GetTempPath(), "age-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes"));

        try
        {
            File.WriteAllText(Path.Combine(root, "Prototypes", "thing.yml"), document);

            foreach ((string path, string content) in files)
            {
                string file = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, content);
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

    /// <summary>Lints the sheets of a game root that a test fills in itself, which is how the folder that is not there is tested.</summary>
    private static LintReport LintSheets(Action<string> fill)
    {
        string root = Path.Combine(Path.GetTempPath(), "age-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            fill(root);

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

    [Fact]
    public void ContentLinter_AStringThatTheBaseLanguageDoesNotHold_IsReported()
    {
        LintReport report = LintLocale(
            "- type: entity\n  id: Fine\n",
            ("en/Entities/creatures.yml", "ent-Fine: fine\nent-Fine.desc: a fine thing\n"),
            ("ru/Entities/creatures.yml", "ent-Fine: ладно\nent-Fine.desc: ладная вещь\nent-Nowhere: нигде\n"));

        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("'ent-Nowhere'").And.Contain("'en'");
    }

    [Fact]
    public void ContentLinter_AStringThatAPrototypeNamesAndNothingAnswers_IsReported()
    {
        LintReport report = LintLocale(
            "- type: entity\n  id: Goblin\n  name: ent-Nothing\n",
            ("en/Entities/creatures.yml", "ent-Goblin: goblin\nent-Goblin.desc: A small, mean creature.\n"));

        report.Problems.Should().ContainSingle().Which.Message.Should().Contain("'ent-Nothing'").And.Contain("'Goblin'");
    }

    [Fact]
    public void ContentLinter_WhatTheEngineShips_HasStringsForEveryName()
    {
        using ServiceProvider provider = Create();
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));

        LintReport content = linter.Lint("Prototypes");
        LintReport strings = linter.LintLocales("Locale");

        content.IsClean.Should().BeTrue();
        strings.IsClean.Should().BeTrue("every entity of the engine is named by a string of the base language");
        strings.Count.Should().BeGreaterThan(8, "the engine ships the strings of its creatures, its items and its window");
    }

    /// <summary>Lints the strings of a game root of its own, together with the prototype that names them.</summary>
    private static LintReport LintLocale(string prototype, params (string Path, string Text)[] documents)
    {
        string root = Path.Combine(Path.GetTempPath(), "age-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Prototypes"));

        try
        {
            File.WriteAllText(Path.Combine(root, "Prototypes", "thing.yml"), prototype);

            foreach ((string path, string text) in documents)
            {
                string file = Path.Combine(root, "Locale", path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, text);
            }

            using ServiceProvider provider = Create();
            var assets = new NullAssetLoader();
            assets.Initialize(root);
            var linter = new ContentLinter(ReadContent(provider), provider.GetRequiredService<ComponentRegistry>(), assets, new StbImageLoader(assets));
            LintReport content = linter.Lint("Prototypes");
            LintReport strings = linter.LintLocales("Locale");

            return new LintReport(content.Count, [.. content.Problems, .. strings.Problems]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
