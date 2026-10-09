using Age.Assets;
using Age.Content.Locale;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class LocaleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "age-locale-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LocaleReader_ADocument_HoldsTheTextOfAKeyAndWhatItSaysBesidesIt()
    {
        IReadOnlyDictionary<string, LocaleString> strings = LocaleReader.Read(
            "ent-Goblin: goblin\n"
            + "ent-Goblin.desc: A small, mean creature.\n"
            + "ent-GoblinHeavy: \"{ ent-Goblin }\"\n",
            "creatures.yml");

        strings["ent-Goblin"].Text.Should().Be("goblin");
        strings["ent-Goblin"].Attributes["desc"].Should().Be("A small, mean creature.");
        strings["ent-GoblinHeavy"].Text.Should().Be("{ ent-Goblin }", "a reference is followed when the language is read, not when it is asked for");
        strings["ent-GoblinHeavy"].File.Should().Be("creatures.yml");
        strings["ent-GoblinHeavy"].Line.Should().Be(3);
        strings["ent-Goblin"].HasForms.Should().BeFalse();
    }

    [Fact]
    public void LocaleReader_ATextAndTheFormsOfIt_AreRefused()
    {
        Action read = () => LocaleReader.Read("ui-items: item\nui-items.one: one item\n", "window.yml");

        read.Should().Throw<LocaleException>().WithMessage("*'ui-items'*writes its text and writes it by count*");
    }

    [Fact]
    public void LocaleReader_AKeyWrittenTwice_IsRefused()
    {
        Action read = () => LocaleReader.Read("ent-Goblin: goblin\nent-Goblin: hobgoblin\n", "creatures.yml");

        read.Should().Throw<LocaleException>().WithMessage("*'ent-Goblin'*more than once*");
    }

    [Fact]
    public void LocaleReader_AKeyThatHoldsAList_IsRefused()
    {
        Action read = () => LocaleReader.Read("ent-Goblin:\n  - goblin\n", "creatures.yml");

        read.Should().Throw<LocaleException>().WithMessage("*'ent-Goblin'*where the string itself is expected*");
    }

    [Fact]
    public void PluralRules_ACount_SelectsTheFormThatItsLanguageWrites()
    {
        PluralRules.Category("en", 1).Should().Be(PluralCategory.One);
        PluralRules.Category("en", 0).Should().Be(PluralCategory.Other);
        PluralRules.Category("ru", 1).Should().Be(PluralCategory.One);
        PluralRules.Category("ru", 21).Should().Be(PluralCategory.One, "twenty-one ends in one and is not eleven");
        PluralRules.Category("ru", 3).Should().Be(PluralCategory.Few);
        PluralRules.Category("ru", 11).Should().Be(PluralCategory.Many, "eleven is not a few however it ends");
        PluralRules.Category("ru", 5).Should().Be(PluralCategory.Many);
        PluralRules.Category("de", 1).Should().Be(PluralCategory.Other, "a language the engine does not know writes the form every language has");
    }

    [Fact]
    public void LocaleService_AReference_AnswersWithTheStringOfTheKeyItNames()
    {
        LocaleService locale = Service((
            "en/Entities/creatures.yml",
            "ent-Goblin: goblin\nent-Goblin.desc: A small, mean creature.\nent-GoblinHeavy: \"{ ent-Goblin }\"\nent-GoblinHeavy.desc: \"{ ent-Goblin.desc }\"\n"));

        locale.Get("ent-GoblinHeavy").Should().Be("goblin");
        locale.Get("ent-GoblinHeavy.desc").Should().Be("A small, mean creature.");
        locale.Missing.Should().BeEmpty();
    }

    [Fact]
    public void LocaleService_AKeyThatIsNotThere_IsTheKeyItself()
    {
        LocaleService locale = Service(("en/Ui/window.yml", "ui-title: Make a choice\n"));

        locale.Get("ui-nothing").Should().Be("ui-nothing", "what is missing is visible rather than empty");
        locale.Get("ui-nothing").Should().Be("ui-nothing");

        locale.Missing.Should().ContainSingle("a key that is missing is reported once however often it is asked for");
        locale.TryGet("ui-nothing", out string text).Should().BeFalse();
        text.Should().Be("ui-nothing");
    }

    [Fact]
    public void LocaleService_ALanguageThatLacksAKey_FallsBackToTheBaseLanguage()
    {
        LocaleService locale = Service(
            ("en/Entities/creatures.yml", "ent-Goblin: goblin\n"),
            ("ru/Entities/creatures.yml", "ent-Other: другое\n"));

        locale.Language = "ru";

        locale.Get("ent-Goblin").Should().Be("goblin", "a translation that is not finished shows the base language rather than a key");
        locale.Get("ent-Other").Should().Be("другое");
        locale.Has("ent-Goblin").Should().BeTrue();
        locale.Has("ent-Goblin", "ru").Should().BeFalse();
    }

    [Fact]
    public void LocaleService_ATextWrittenByCount_PicksTheFormOfItsLanguage()
    {
        LocaleService locale = Service((
            "ru/Ui/window.yml",
            "ui-entities.one: \"{count} сущность\"\nui-entities.few: \"{count} сущности\"\nui-entities.many: \"{count} сущностей\"\nui-entities.other: \"{count} сущности\"\n"));

        locale.Language = "ru";

        locale.Get("ui-entities", ("count", 1)).Should().Be("1 сущность");
        locale.Get("ui-entities", ("count", 3)).Should().Be("3 сущности");
        locale.Get("ui-entities", ("count", 5)).Should().Be("5 сущностей");
    }

    [Fact]
    public void LocaleService_APlaceholder_WritesTheValueTheCallPassed()
    {
        LocaleService locale = Service(("en/Ui/window.yml", "ui-hello: Hello, {name}!\n"));

        locale.Get("ui-hello", ("name", "goblin")).Should().Be("Hello, goblin!");
        locale.Get("ui-hello").Should().Be("Hello, {name}!", "a placeholder that nothing fills stays visible");
        locale.Missing.Should().ContainSingle();
    }

    [Fact]
    public void Locale_WhatTheEngineShips_IsReadInBothLanguages()
    {
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));
        var locale = new LocaleService(assets);

        locale.Languages.Should().BeEquivalentTo("en", "ru");
        locale.Get("ent-Goblin").Should().Be("goblin");
        locale.Get("ui-entities", ("count", 1)).Should().Be("1 entity");
        locale.Get("ui-entities", ("count", 4)).Should().Be("4 entities");

        locale.Language = "ru";

        locale.Get("ent-Goblin").Should().Be("гоблин");
        locale.Get("ent-Goblin.desc").Should().Contain("кусает");
        locale.Get("ui-entities", ("count", 1)).Should().Be("1 сущность");
        locale.Get("ui-entities", ("count", 3)).Should().Be("3 сущности");
        locale.Get("ui-entities", ("count", 5)).Should().Be("5 сущностей");
        locale.Missing.Should().BeEmpty();
    }

    /// <summary>Builds a service over a game root of its own, which the test writes the documents of a language into.</summary>
    private LocaleService Service(params (string Path, string Text)[] documents)
    {
        foreach ((string path, string text) in documents)
        {
            string file = Path.Combine(_root, LocaleService.Folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }

        var assets = new NullAssetLoader();
        assets.Initialize(_root);

        return new LocaleService(assets);
    }
}
