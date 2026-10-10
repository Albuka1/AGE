using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ConsoleServiceTests
{
    [Fact]
    public void ConsoleService_Execute_RunsARegisteredCommandWithItsArguments()
    {
        var console = new ConsoleService();
        var seen = new List<string>();

        console.Register("spawn", "Puts n sprites on screen.", seen.AddRange);

        console.Execute("spawn 3 big").Should().BeTrue();

        seen.Should().Equal("3", "big");
    }

    [Fact]
    public void ConsoleService_Execute_NamesTheCommandItDoesNotKnow()
    {
        var console = new ConsoleService();

        console.Execute("nope").Should().BeFalse();

        console.Output.Should().ContainSingle().Which.Should().StartWith("error:").And.Contain("nope");
    }

    [Fact]
    public void ConsoleService_Execute_MatchesANameWithoutRegardToCase()
    {
        var console = new ConsoleService();
        var ran = false;

        console.Register("spawn", "Puts sprites on screen.", _ => ran = true);

        console.Execute("SPAWN").Should().BeTrue();

        ran.Should().BeTrue();
    }

    [Fact]
    public void ConsoleService_Register_StartsWithTheTwoCommandsThatAreAlwaysThere()
    {
        var console = new ConsoleService();

        console.Commands.Select(command => command.Name).Should().Equal("help", "clear");
    }

    [Fact]
    public void ConsoleService_Help_ListsEveryCommandOfTheConsole()
    {
        var console = new ConsoleService();

        console.Register("spawn", "Puts sprites on screen.", _ => { });
        console.Execute("help");

        console.Output.Should().Contain(line => line.StartsWith("spawn - ", StringComparison.Ordinal));
        console.Output.Should().Contain(line => line.StartsWith("help - ", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsoleService_Submit_RunsTheTypedLineAndRemembersIt()
    {
        var console = new ConsoleService();
        var ran = new List<string>();

        console.Register("spawn", "Puts sprites on screen.", arguments => ran.Add(string.Join(' ', arguments)));

        foreach (char character in "spawn 2")
        {
            console.Type(character);
        }

        console.Input.Should().Be("spawn 2");
        console.Submit().Should().BeTrue();
        console.Input.Should().BeEmpty("the line was run");
        console.History.Should().Equal("spawn 2");
        ran.Should().Equal("2");
    }

    [Fact]
    public void ConsoleService_Submit_AnEmptyLineRunsNothingAndRemembersNothing()
    {
        var console = new ConsoleService();

        console.Type(' ');
        console.Submit().Should().BeFalse();

        console.History.Should().BeEmpty();
    }

    [Fact]
    public void ConsoleService_TypeAndBackspace_ChangeTheLineThatIsBeingTyped()
    {
        var console = new ConsoleService();

        console.Type('a');
        console.Type('\n');
        console.Type('b');
        console.Type('c');
        console.Backspace();

        console.Input.Should().Be("ab", "a control character never belongs to a line that is typed");

        console.Backspace();
        console.Backspace();
        console.Backspace();

        console.Input.Should().BeEmpty("an empty line loses nothing");
    }

    [Fact]
    public void ConsoleService_Recall_WalksTheHistoryAndEndsOnAnEmptyLine()
    {
        var console = new ConsoleService();

        Enter(console, "first");
        Enter(console, "second");
        console.Type('x');

        console.RecallPrevious();
        console.Input.Should().Be("second");

        console.RecallPrevious();
        console.Input.Should().Be("first");

        console.RecallPrevious();
        console.Input.Should().Be("first", "the oldest line is the end of the history");

        console.RecallNext();
        console.Input.Should().Be("second");

        console.RecallNext();
        console.Input.Should().BeEmpty();
    }

    [Fact]
    public void ConsoleService_OutputCapacity_DropsTheOldestLines()
    {
        var console = new ConsoleService { OutputCapacity = 2 };

        console.Write("one");
        console.Write("two");
        console.WriteError("three");

        console.Output.Should().Equal("two", "error: three");
    }

    [Fact]
    public void ConsoleService_Register_RejectsANameThatIsTakenOrHoldsWhitespace()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        Action taken = () => console.Register("spawn", "Again.", _ => { });
        Action spaced = () => console.Register("two words", "Nothing.", _ => { });
        Action blank = () => console.Register(" ", "Nothing.", _ => { });

        taken.Should().Throw<ArgumentException>().WithMessage("*already registered*");
        spaced.Should().Throw<ArgumentException>().WithMessage("*whitespace*");
        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConsoleService_Unregister_RemovesOnlyWhatWasRegistered()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        console.Unregister("spawn").Should().BeTrue();
        console.Unregister("spawn").Should().BeFalse();
        console.Execute("spawn").Should().BeFalse();

        console.Commands.Should().NotContain(command => command.Name == "spawn");
    }

    [Fact]
    public void ConsoleService_Toggle_OpensAndClosesTheConsole()
    {
        var console = new ConsoleService();

        console.IsOpen.Should().BeFalse();

        console.Toggle();
        console.IsOpen.Should().BeTrue();

        console.Close();
        console.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ConsoleService_Clear_RemovesTheOutputAndKeepsTheHistory()
    {
        var console = new ConsoleService();

        console.Write("something");
        Enter(console, "help");
        console.Clear();

        console.Output.Should().BeEmpty();
        console.History.Should().Equal("help");
    }

    [Fact]
    public async Task ConsoleService_WriteFromAnotherThread_ReachesTheOutput()
    {
        var console = new ConsoleService();

        await Task.Run(
            () =>
            {
                for (var index = 0; index < 200; index++)
                {
                    console.Write($"line {index}");
                }
            },
            TestContext.Current.CancellationToken);

        console.Output.Should().HaveCount(200, "a loader may write while the frame thread draws the lines");
    }

    [Fact]
    public void ConsoleService_Execute_ACommandThatThrows_ReportsItAndAnswersFalse()
    {
        var console = new ConsoleService();

        console.Register("boom", "A command that fails.", _ => throw new InvalidOperationException("the command failed"));

        console.Execute("boom").Should().BeFalse();
        console.Output.Should().ContainSingle().Which.Should().StartWith("error:").And.Contain("boom").And.Contain("the command failed");

        console.Execute("help").Should().BeTrue("the console keeps working after a command failed");
    }

    [Fact]
    public void ConsoleService_Lines_CarryTheLevelOfTheLine()
    {
        var console = new ConsoleService();

        console.Write("plain");
        console.WriteWarning("careful");
        console.WriteError("broken");

        console.Lines.Should().HaveCount(3);
        console.Lines[0].Should().Be(new ConsoleLine("plain", ConsoleLevel.Normal));
        console.Lines[1].Should().Be(new ConsoleLine("careful", ConsoleLevel.Warning));
        console.Lines[2].Should().Be(new ConsoleLine("error: broken", ConsoleLevel.Error));
    }

    [Fact]
    public void ConsoleService_Lines_AndOutput_HoldTheSameText()
    {
        var console = new ConsoleService();

        console.Write("plain");
        console.WriteError("broken");

        console.Lines.Select(line => line.Text).Should().Equal(console.Output);
    }

    [Fact]
    public void ConsoleService_Hint_AnswersTheCommandTheLineNames()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        console.Type('s');
        console.Type('p');
        console.Type('a');
        console.Type('w');
        console.Type('n');

        console.Hint.Should().NotBeNull();
        console.Hint!.Value.Description.Should().Be("Puts sprites on screen.");
    }

    [Fact]
    public void ConsoleService_Hint_IgnoresTheArguments()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        foreach (char character in "spawn 3")
        {
            console.Type(character);
        }

        console.Hint!.Value.Name.Should().Be("spawn", "a line still names its command when it holds arguments");
    }

    [Fact]
    public void ConsoleService_Hint_AnswersNothingForAWordNoCommandMatches()
    {
        var console = new ConsoleService();

        console.Type('z');

        console.Hint.Should().BeNull();
    }

    [Fact]
    public void ConsoleService_Complete_CompletesAWordThatMatchesOneCommand()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        console.Type('s');
        console.Type('p');
        console.Type('a');

        console.Complete().Should().BeTrue();
        console.Input.Should().Be("spawn");
    }

    [Fact]
    public void ConsoleService_Complete_CompletesToThePartSeveralNamesShare()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });
        console.Register("spawnMany", "Puts many sprites on screen.", _ => { });

        console.Type('s');
        console.Type('p');
        console.Type('a');

        console.Complete().Should().BeTrue();
        console.Input.Should().Be("spawn", "the shared start of the two names is what a word is completed to");
    }

    [Fact]
    public void ConsoleService_Complete_LeavesAWholeNameAlone()
    {
        var console = new ConsoleService();

        Enter(console, "help");

        console.Complete().Should().BeFalse("a word that is already a whole name is nothing to complete");
        console.Input.Should().BeEmpty();
    }

    [Fact]
    public void ConsoleService_Complete_IgnoresTheWordsBehindTheFirstOne()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        foreach (char character in "spawn s")
        {
            console.Type(character);
        }

        console.Complete().Should().BeFalse("only the first word of a line is a command name");
        console.Input.Should().Be("spawn s");
    }

    /// <summary>Types a line and runs it, which is what a developer at the console does.</summary>
    private static void Enter(ConsoleService console, string line)
    {
        foreach (char character in line)
        {
            console.Type(character);
        }

        console.Submit();
    }
}
