using Age.Content.Sheets;
using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SpriteSheetReaderTests
{
    [Fact]
    public void SpriteSheetReader_ADocument_IsReadIntoTheGridAndItsStates()
    {
        SpriteSheet sheet = SpriteSheetReader.Read(Document, "goblin.yml");

        sheet.Image.Should().Be("Textures/Entities/goblin.bmp");
        sheet.Cell.Should().Be(new Vector2(16f, 16f));
        sheet.Columns.Should().Be(4);
        sheet.Rows.Should().Be(3);
        sheet.Count.Should().Be(3);
        sheet.License.Should().Be("MIT");
        sheet.Copyright.Should().Be("AGE, drawn for this repository");

        sheet.TryGetState("idle", out SpriteSheetState? idle).Should().BeTrue();
        idle!.Row.Should().Be(0);
        idle.Frames.Should().Be(4);
        idle.Delay.Should().Be(0.15f);
        idle.Loop.Should().BeTrue("a state that says nothing about looping keeps playing");

        sheet.TryGetState("walk", out SpriteSheetState? walk).Should().BeTrue();
        walk!.Delay.Should().Be(0.1f, "the length of a frame has a default");

        sheet.TryGetState("attack", out SpriteSheetState? attack).Should().BeTrue();
        attack!.Row.Should().Be(2);
        attack.Frames.Should().Be(3);
        attack.Loop.Should().BeFalse();
        sheet.TryGetState("jump", out _).Should().BeFalse();
    }

    [Fact]
    public void SpriteSheetReader_AFrame_IsOneCellOfTheGrid()
    {
        SpriteSheet sheet = SpriteSheetReader.Read(Document, "goblin.yml");
        sheet.TryGetState("attack", out SpriteSheetState? attack).Should().BeTrue();

        Rect frame = sheet.Region(attack!, frame: 2);

        frame.X.Should().Be(0.5f, "the third of three frames of a row of four cells starts halfway");
        frame.Y.Should().Be(2f / 3f, "the state lies on the third of three rows");
        frame.Width.Should().Be(0.25f);
        frame.Height.Should().Be(1f / 3f);
    }

    [Fact]
    public void SpriteSheetReader_AStateThatGivesALength_PlaysEveryFrameOnIt()
    {
        SpriteSheet sheet = SpriteSheetReader.Read(Document, "goblin.yml");

        sheet.States["idle"].Delays.Should().Equal([0.15f, 0.15f, 0.15f, 0.15f], "a state that gives one length gives it for every frame of it");
    }

    [Fact]
    public void SpriteSheetReader_AStateThatGivesALengthPerFrame_PlaysEveryFrameOnItsOwn()
    {
        string document = Document.Replace("delay: 0.08", "delays:\n      - 0.18\n      - 0.08\n      - 0.15", StringComparison.Ordinal);

        SpriteSheet sheet = SpriteSheetReader.Read(document, "goblin.yml");
        SpriteSheetState attack = sheet.States["attack"];

        attack.Delays.Should().Equal(0.18f, 0.08f, 0.15f);
        attack.DelayOf(0).Should().Be(0.18f, "a wind-up is slower than the strike that follows it");
        attack.DelayOf(1).Should().Be(0.08f);
        attack.DelayOf(2).Should().Be(0.15f);
        attack.DelayOf(9).Should().Be(0.15f, "a frame outside the state takes the length of the frame nearest to it");
    }

    [Fact]
    public void SpriteSheetReader_ALengthPerFrameThatDoesNotCoverEveryFrame_IsReported()
    {
        string document = Document.Replace("delay: 0.08", "delays:\n      - 0.18\n      - 0.08", StringComparison.Ordinal);

        Action read = () => SpriteSheetReader.Read(document, "goblin.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*holds 3 frames*'delays' gives 2*");
    }

    [Fact]
    public void SpriteSheetReader_ALengthAndALengthPerFrame_AreReported()
    {
        string document = Document.Replace("delay: 0.08", "delay: 0.1\n    delays:\n      - 0.18\n      - 0.08\n      - 0.15", StringComparison.Ordinal);

        Action read = () => SpriteSheetReader.Read(document, "goblin.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*with 'delay' or with 'delays'*");
    }

    [Fact]
    public void SpriteSheetReader_ALengthPerFrameThatIsNotAList_IsReported()
    {
        string document = Document.Replace("delay: 0.08", "delays: 0.1", StringComparison.Ordinal);

        Action read = () => SpriteSheetReader.Read(document, "goblin.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*'delays' holds one length in seconds per frame*");
    }

    [Fact]
    public void SpriteSheetReader_AStateOutsideTheGrid_IsReported()
    {
        const string Outside = """
            version: 1
            image: art.bmp
            cell:
              X: 8
              Y: 8
            columns: 2
            rows: 1
            states:
              idle:
                row: 1
                frames: 2
            """;

        Action read = () => SpriteSheetReader.Read(Outside, "outside.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*row 1*holds 1 rows*outside.yml*");
    }

    [Fact]
    public void SpriteSheetReader_AStateWithMoreFramesThanItsRow_IsReported()
    {
        const string Wide = """
            version: 1
            image: art.bmp
            cell:
              X: 8
              Y: 8
            columns: 2
            rows: 1
            states:
              idle:
                row: 0
                frames: 3
            """;

        Action read = () => SpriteSheetReader.Read(Wide, "wide.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*3 frames*holds 2 cells*");
    }

    [Fact]
    public void SpriteSheetReader_AFieldAStateDoesNotHave_IsReported()
    {
        const string Unknown = """
            version: 1
            image: art.bmp
            cell:
              X: 8
              Y: 8
            columns: 2
            rows: 1
            states:
              idle:
                row: 0
                frames: 2
                speed: 2
            """;

        Action read = () => SpriteSheetReader.Read(Unknown, "unknown.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*'speed' is not a field*");
    }

    [Fact]
    public void SpriteSheetReader_ADocumentThatIsNotASheet_IsReported()
    {
        Action read = () => SpriteSheetReader.Read("- a list rather than a sheet\n", "list.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*a set of fields*a list*list.yml*");
    }

    [Fact]
    public void SpriteSheetReader_ADocumentThatNamesNothing_IsReported()
    {
        Action read = () => SpriteSheetReader.Read("version: 1\ncolumns: 2\n", "empty.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*names the image*empty.yml*");
    }

    [Fact]
    public void SpriteSheetReader_ADocumentWithoutAVersion_IsReported()
    {
        Action read = () => SpriteSheetReader.Read(Document.Replace("version: 1\n", string.Empty, StringComparison.Ordinal), "no-version.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*says which version*no-version.yml*");
    }

    [Fact]
    public void SpriteSheetReader_ADocumentOfAnotherVersion_IsReported()
    {
        Action read = () => SpriteSheetReader.Read(Document.Replace("version: 1", "version: 2", StringComparison.Ordinal), "future.yml");

        read.Should().Throw<SpriteSheetException>().WithMessage("*version 2*reads version 1*");
    }

    [Fact]
    public void SpriteSheetReader_ADocumentWithoutALicence_IsReadWithoutOne()
    {
        string document = Document
            .Replace("license: MIT\n", string.Empty, StringComparison.Ordinal)
            .Replace("copyright: AGE, drawn for this repository\n", string.Empty, StringComparison.Ordinal);

        SpriteSheet sheet = SpriteSheetReader.Read(document, "bare.yml");

        sheet.License.Should().BeNull("what art belongs to is a rule of a build rather than of the format, and the linter is what checks it");
        sheet.Copyright.Should().BeNull();
    }

    /// <summary>A document of a sheet, which the tests of the reader and of the services share.</summary>
    internal const string Document =
        "version: 1\n"
        + "license: MIT\n"
        + "copyright: AGE, drawn for this repository\n"
        + "image: Textures/Entities/goblin.bmp\n"
        + "cell:\n"
        + "  X: 16\n"
        + "  Y: 16\n"
        + "columns: 4\n"
        + "rows: 3\n"
        + "states:\n"
        + "  idle:\n"
        + "    row: 0\n"
        + "    frames: 4\n"
        + "    delay: 0.15\n"
        + "  walk:\n"
        + "    row: 1\n"
        + "    frames: 4\n"
        + "  attack:\n"
        + "    row: 2\n"
        + "    frames: 3\n"
        + "    delay: 0.08\n"
        + "    loop: false\n";
}
