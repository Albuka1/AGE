using System.Globalization;
using Age.Assets;
using Age.Content.Locale;
using Age.Content.Prototypes;
using Age.Core;
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
        var locale = new LocaleService(assets, null, CultureInfo.InvariantCulture);

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

    [Fact]
    public void LocaleService_AName_IsDrawnFromTheKeyOfTheLanguageThenTheWordsOfTheDocumentThenTheIdentifier()
    {
        LocaleService locale = Service(
            ("en/Entities/creatures.yml", "ent-Goblin: goblin\nent-Goblin.desc: A small, mean creature.\n"),
            ("ru/Entities/creatures.yml", "ent-Orc: орк\n"));

        PrototypeManager prototypes = Prototypes(
            "- type: entity\n  id: Goblin\n"
            + "- type: entity\n  id: GoblinNamed\n  name: Gobby\n  desc: The words of the document.\n"
            + "- type: entity\n  id: Orc\n  name: Orc\n  desc: A big, mean creature.\n"
            + "- type: entity\n  id: Runt\n");

        // English is the base language, so every key it holds is drawn: the words of a document are the fallback of a key that is not
        // there, and they are not what is drawn here because the language holds the key.
        locale.NameOf(prototypes.Get<EntityPrototype>("Goblin")).Should().Be("goblin");
        locale.Describe(prototypes.Get<EntityPrototype>("Goblin")).Should().Be("A small, mean creature.");

        // A key that no language holds falls back to the words the document wrote, so a thing that is not translated needs no string.
        locale.NameOf(prototypes.Get<EntityPrototype>("GoblinNamed")).Should().Be("Gobby");
        locale.Describe(prototypes.Get<EntityPrototype>("GoblinNamed")).Should().Be("The words of the document.");

        // A key and no words at all is drawn as the identifier, which is the last step of the chain rather than nothing.
        locale.NameOf(prototypes.Get<EntityPrototype>("Runt")).Should().Be("Runt");

        // A language that holds the key answers it, and one that holds none takes the words of the document rather than the base
        // language, because the chain asks the base language before the words and it holds no key for that entity either.
        locale.Language = "ru";

        locale.NameOf(prototypes.Get<EntityPrototype>("Orc")).Should().Be("орк");
        locale.NameOf(prototypes.Get<EntityPrototype>("GoblinNamed")).Should().Be("Gobby", "neither language holds the key, so the document answers");
        locale.Describe(prototypes.Get<EntityPrototype>("GoblinNamed")).Should().Be("The words of the document.");
        locale.NameOf(prototypes.Get<EntityPrototype>("Goblin")).Should().Be("goblin", "a language that lacks the key falls back to the base language");

        // A name that falls back is a thing a game expected rather than a key that is missing, so nothing is reported.
        locale.Missing.Should().BeEmpty();
    }

    /// <summary>Reads the documents of a content of entities, which is what the words of a name live in.</summary>
    private static PrototypeManager Prototypes(params string[] documents)
    {
        var components = new ComponentRegistry();
        var prototypes = new PrototypeManager(components);

        prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);

        foreach (string document in documents)
        {
            prototypes.Add("entities.yml", document);
        }

        prototypes.Build();

        return prototypes;
    }

    /// <summary>Builds a service over a game root of its own, which the test writes the documents of a language into.</summary>
    private LocaleService Service(params (string Path, string Text)[] documents) => Build(CultureInfo.InvariantCulture, documents);

    /// <summary>Builds a service with the culture that decides what it plays in, over a game root of its own.</summary>
    private LocaleService Service(CultureInfo culture, params (string Path, string Text)[] documents) => Build(culture, documents);

    /// <summary>Writes the documents of a language into a game root of its own and builds a service over it.</summary>
    private LocaleService Build(CultureInfo culture, (string Path, string Text)[] documents)
    {
        foreach ((string path, string text) in documents)
        {
            string file = Path.Combine(_root, LocaleService.Folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }

        var assets = new NullAssetLoader();
        assets.Initialize(_root);

        return new LocaleService(assets, null, culture);
    }

    [Fact]
    public void Locale_ABuildBeforeTheLoaderKnowsItsRoot_ReadsTheDocumentsOnceItDoes()
    {
        string folder = Path.Combine(_root, LocaleService.Folder, "en", "Ui");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "window.yml"), "ui-title: Make a choice\n");

        var assets = new NullAssetLoader();

        // A host that makes the service before it says where its files are, which is what a game whose first loading step is the
        // one that initializes the loader does: the answer that is taken now is empty, and the fix is that it is not remembered.
        var locale = new LocaleService(assets, null, CultureInfo.InvariantCulture);
        locale.Get("ui-title").Should().Be("ui-title", "the loader has no root yet, so nothing can be read");

        assets.Initialize(_root);

        locale.Get("ui-title").Should().Be("Make a choice", "the first real ask reads the documents rather than the empty answer that was taken before");
        locale.Count.Should().Be(1);
    }

    [Fact]
    public void Locale_StartsInTheLanguageOfTheSystemThatTheGameShips()
    {
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));

        var russian = new LocaleService(assets, null, CultureInfo.GetCultureInfo("ru-RU"));
        var german = new LocaleService(assets, null, CultureInfo.GetCultureInfo("de-DE"));

        russian.SystemLanguage.Should().Be("ru");
        russian.Language.Should().Be("ru", "the game ships the language the system is set to");
        russian.Get("ent-Goblin").Should().Be("гоблин");

        german.SystemLanguage.Should().Be("en", "the game ships no German, so the base language answers");
        german.Language.Should().Be("en");
        german.Get("ent-Goblin").Should().Be("goblin");
    }

    [Fact]
    public void Locale_LanguageTheGameShipsUnderItsWholeName_IsFound()
    {
        LocaleService locale = Service(
            CultureInfo.GetCultureInfo("pt-BR"),
            ("en/Entities/creatures.yml", "ent-Goblin: goblin\n"),
            ("pt-BR/Entities/creatures.yml", "ent-Goblin: goblinzinho\n"));

        locale.SystemLanguage.Should().Be("pt-BR", "the whole name of the culture is what a game names the folder of one translation");
        locale.Get("ent-Goblin").Should().Be("goblinzinho");
    }
}
