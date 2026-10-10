using System.Text.Json;
using Age.Content;
using Age.Content.Yaml;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class YamlReaderTests
{
    [Fact]
    public void YamlReader_Read_MakesNamesAndValuesOfAMapping()
    {
        const string Text = """
            id: pistol
            damage: 12
            """;

        YamlValue document = YamlReader.Read(Text, "pistol.yml");

        YamlMapping mapping = document.Should().BeOfType<YamlMapping>().Subject;
        mapping.Entries.Select(entry => entry.Name).Should().Equal("id", "damage");
        mapping.TryGet("id", out YamlValue id).Should().BeTrue();
        id.Should().BeOfType<YamlScalar>().Which.Text.Should().Be("pistol");
        mapping.TryGet("damage", out YamlValue damage).Should().BeTrue();
        damage.Line.Should().Be(2);
    }

    [Fact]
    public void YamlReader_Read_KeepsTheNestingOfBlocks()
    {
        const string Text = """
            parent: WeaponBase
            Transform:
              Position:
                X: 1.5
                Y: -2
              Rotation: 90
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "pistol.yml");

        YamlMapping transform = mapping.TryGet("Transform", out YamlValue found) ? (YamlMapping)found : throw new InvalidOperationException();
        YamlMapping position = (YamlMapping)transform.Entries.Single(entry => entry.Name == "Position").Value;

        position.Entries.Select(entry => entry.Name).Should().Equal("X", "Y");
        ((YamlScalar)position.Entries[0].Value).Text.Should().Be("1.5");
        ((YamlScalar)position.Entries[1].Value).Text.Should().Be("-2");
        ((YamlScalar)transform.Entries[1].Value).Text.Should().Be("90");
    }

    [Fact]
    public void YamlReader_Read_MakesAListOfWords()
    {
        const string Text = """
            tags:
              - weapon
              - ranged
              - forbidden
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "pistol.yml");
        YamlSequence tags = (YamlSequence)mapping.Entries[0].Value;

        tags.Items.Should().HaveCount(3);
        tags.Items.Select(item => ((YamlScalar)item).Text).Should().Equal("weapon", "ranged", "forbidden");
        tags.Items[0].Line.Should().Be(2);
    }

    [Fact]
    public void YamlReader_Read_MakesAListOfMappingsThatStartBehindTheirDash()
    {
        const string Text = """
            slots:
              - slot: head
                size: 1
              - slot: body
                size: 2
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "armour.yml");
        YamlSequence slots = (YamlSequence)mapping.Entries[0].Value;

        slots.Items.Should().HaveCount(2);
        ((YamlMapping)slots.Items[0]).Entries.Select(entry => entry.Name).Should().Equal("slot", "size");
        ((YamlScalar)((YamlMapping)slots.Items[1]).Entries[0].Value).Text.Should().Be("body");
    }

    [Fact]
    public void YamlReader_Read_LeavesANameWithoutAValueEmpty()
    {
        const string Text = """
            description:
            id: sword
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "sword.yml");

        mapping.Entries[0].Value.Should().BeOfType<YamlScalar>().Which.IsEmpty.Should().BeTrue();
        mapping.Entries[1].Value.Should().BeOfType<YamlScalar>().Which.Text.Should().Be("sword");
    }

    [Fact]
    public void YamlReader_Read_TakesACommentOutOfALineAndLeavesAHashInsideAValue()
    {
        const string Text = """
            # what the weapon does
            id: pistol  # the id of the weapon
            label: "a # b"
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "pistol.yml");

        mapping.Entries.Should().HaveCount(2);
        ((YamlScalar)mapping.Entries[0].Value).Text.Should().Be("pistol");
        ((YamlScalar)mapping.Entries[1].Value).Text.Should().Be("a # b");
    }

    [Fact]
    public void YamlReader_Read_KeepsAQuotedWordAWordAndReadsItsEscapes()
    {
        const string Text = """
            flag: "true"
            flagAgain: true
            quoted: "a \"b\" and a \\ and a\ttab"
            single: 'it''s fine'
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "words.yml");

        YamlScalar quoted = (YamlScalar)mapping.Entries[0].Value;
        quoted.Text.Should().Be("true");
        quoted.Quoted.Should().BeTrue("a word in quotes is a word, whatever it spells");
        ((YamlScalar)mapping.Entries[1].Value).Quoted.Should().BeFalse();
        ((YamlScalar)mapping.Entries[2].Value).Text.Should().Be("a \"b\" and a \\ and a\ttab");
        ((YamlScalar)mapping.Entries[3].Value).Text.Should().Be("it''s fine");
    }

    [Fact]
    public void YamlReader_Read_AnEmptyDocument_HoldsNothing()
    {
        YamlValue document = YamlReader.Read("\n# only a comment\n\n", "empty.yml");

        document.Should().BeOfType<YamlMapping>().Which.Entries.Should().BeEmpty();
    }

    [Fact]
    public void YamlReader_Read_ATabInTheIndentation_IsRefusedWithItsLine()
    {
        const string Text = "id: pistol\n\tname: pistol";

        Action read = () => YamlReader.Read(Text, "pistol.yml");

        YamlException error = read.Should().Throw<YamlException>().Subject.Single();
        error.Line.Should().Be(2);
        error.Message.Should().Contain("tab", "the message says what was wrong, not only where");
    }

    [Fact]
    public void YamlReader_Read_AShapeItDoesNotRead_IsRefusedWithItsLine()
    {
        Action flow = () => YamlReader.Read("tags: [weapon, ranged]", "pistol.yml");
        Action anchor = () => YamlReader.Read("id: &base pistol", "pistol.yml");
        Action tag = () => YamlReader.Read("id: !!str pistol", "pistol.yml");
        Action block = () => YamlReader.Read("text: |\n  a paragraph", "pistol.yml");

        flow.Should().Throw<YamlException>().WithMessage("*block style*");
        anchor.Should().Throw<YamlException>().WithMessage("*anchor*");
        tag.Should().Throw<YamlException>().WithMessage("*tag*");
        block.Should().Throw<YamlException>().WithMessage("*block scalar*");
    }

    [Fact]
    public void YamlReader_Read_TheSameNameTwice_IsRefusedWithItsLine()
    {
        const string Text = """
            id: pistol
            damage: 1
            id: rifle
            """;

        Action read = () => YamlReader.Read(Text, "pistol.yml");

        YamlException error = read.Should().Throw<YamlException>().Subject.Single();
        error.Line.Should().Be(3);
        error.Message.Should().Contain("'id'");
    }

    [Fact]
    public void YamlReader_Read_AQuoteThatNeverEnds_IsRefusedWithItsLine()
    {
        const string Text = """
            id: pistol
            label: "a word without an end
            """;

        Action read = () => YamlReader.Read(Text, "pistol.yml");

        YamlException error = read.Should().Throw<YamlException>().Subject.Single();
        error.Line.Should().Be(2);
        error.Message.Should().Contain("quote");
    }

    [Fact]
    public void YamlReader_Read_KeepsAListInsideAMapping()
    {
        const string Text = """
            a:
              - 1
              - 2
            b: 3
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "numbers.yml");
        YamlSequence numbers = (YamlSequence)mapping.Entries[0].Value;

        numbers.Items.Select(item => ((YamlScalar)item).Text).Should().Equal("1", "2");
        ((YamlScalar)mapping.Entries[1].Value).Text.Should().Be("3", "the list ends where the indentation goes back");
    }

    [Fact]
    public void YamlReader_Read_KeepsAListInsideANestedMapping()
    {
        // A vector of a material is written this way: the numbers of the vector are a block sequence under the kind of the
        // uniform, and the whole of it has to survive into the JSON that a component contract reads.
        const string Text = """
            uniforms:
              Speed:
                float: 4.0
              Direction:
                vec2:
                  - 1.0
                  - 0.0
            """;

        YamlMapping mapping = (YamlMapping)YamlReader.Read(Text, "probe.yml");
        YamlMapping uniforms = (YamlMapping)mapping.Entries[0].Value;
        YamlMapping direction = (YamlMapping)uniforms.Entries[1].Value;

        direction.Entries[0].Value.Should().BeOfType<YamlSequence>();

        JsonElement json = YamlJson.Write(mapping);

        json.GetProperty("uniforms").GetProperty("Direction").GetProperty("vec2").GetArrayLength().Should().Be(2, "the numbers of a vector are a list of two, not a word");
    }

    [Fact]
    public void YamlReader_Read_AWordAndABlockBehindOneName_IsRefused()
    {
        const string Text = """
            id: pistol
              damage: 1
            """;

        Action read = () => YamlReader.Read(Text, "pistol.yml");

        read.Should().Throw<YamlException>().WithMessage("*not both*");
    }

    [Fact]
    public void YamlReader_Read_TwoValuesInOneDocument_IsRefused()
    {
        const string Text = """
            a word
            and another
            """;

        Action read = () => YamlReader.Read(Text, "pistol.yml");

        YamlException error = read.Should().Throw<YamlException>().Subject.Single();
        error.Line.Should().Be(2);
        error.Message.Should().Contain("one value");
    }
}
