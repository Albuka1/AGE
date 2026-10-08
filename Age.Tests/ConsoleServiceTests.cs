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
