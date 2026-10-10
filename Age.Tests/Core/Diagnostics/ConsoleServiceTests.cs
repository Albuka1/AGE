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
    public void ConsoleService_Register_RejectsANameThatIsTakenOrBlank()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        Action taken = () => console.Register("spawn", "Again.", _ => { });
        Action blank = () => console.Register(" ", "Nothing.", _ => { });

        taken.Should().Throw<ArgumentException>().WithMessage("*already registered*");
        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConsoleService_Register_KeepsAWholeNameOfSeveralWordsToTheSameSpacing()
    {
        var console = new ConsoleService();
        console.Register("cvars   set", "Sets a setting.", _ => { });

        Action again = () => console.Register("cvars set", "Again.", _ => { });

        again.Should().Throw<ArgumentException>("the name is stored with one space between its words");
    }

    [Fact]
    public void ConsoleService_Execute_RunsTheCommandOfTheMostWordsThatTheLineNames()
    {
        var console = new ConsoleService();
        var ran = new List<string>();

        console.Register("cvars", "Lists the settings.", _ => ran.Add("list"));
        console.Register("cvars set", "Sets a setting.", arguments => ran.Add($"set {string.Join(' ', arguments)}"));

        console.Execute("cvars set locale ru").Should().BeTrue();
        console.Execute("cvars").Should().BeTrue();

        ran.Should().Equal("set locale ru", "list");
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
    public void ConsoleService_Hint_FindsACommandOfSeveralWords()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Sets a setting.", _ => { });

        foreach (char character in "cvars set")
        {
            console.Type(character);
        }

        console.Hint!.Value.Name.Should().Be("cvars set", "the whole path of the line names the command");
    }

    [Fact]
    public void ConsoleService_Matches_ListTheLevelsThatCouldStillBeTyped()
    {
        var console = new ConsoleService();
        console.Register("cvars", "Lists the settings.", _ => { });
        console.Register("cvars set", "Sets a setting.", _ => { });
        console.Register("cvars get", "Answers a setting.", _ => { });
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        // A line that is being named lists the levels one below it: 'cvars' names the group, and what could be typed at it is the
        // levels that follow, which is what a console of several levels lists at every stage.
        console.Matches("cvars").Select(command => command.Name).Should().Equal("cvars");
        console.Matches("cvars ").Select(command => command.Name).Should().Equal("cvars set", "cvars get");
        console.Matches("cvars s").Select(command => command.Name).Should().Equal("cvars set");
    }

    [Fact]
    public void ConsoleService_Matches_ALineThatWalkedIntoAGroup_ListsTheLevelsBelowIt()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Sets a setting.", _ => { });
        console.Register("cvars get", "Answers a setting.", _ => { });

        // The path before the last word is walked into the tree, and the levels below it are the rows, so each word of a command is
        // suggested in turn rather than the whole name at once.
        console.Matches("cv").Select(command => command.Name).Should().Equal("cvars");
        console.Matches("cvars ").Select(command => command.Name).Should().Equal("cvars set", "cvars get");
        console.Matches("cvars set").Select(command => command.Name).Should().Equal(new[] { "cvars set" });
    }

    [Fact]
    public void ConsoleService_Execute_WalksTheGroupsOfACommandOfSeveralLevels()
    {
        var console = new ConsoleService();
        var seen = new List<string>();

        // 'set' is a level that is never registered on its own: registering 'cvars set' makes 'cvars' and 'set' groups that a line
        // walks through, which is what makes a command of several levels a tree rather than a name with spaces in it.
        console.Register("cvars", "Lists the settings.", _ => seen.Add("list"));
        console.Register("cvars set", "Writes a setting.", arguments => seen.Add(string.Join(' ', arguments)));

        console.Execute("cvars").Should().BeTrue();
        console.Execute("cvars set locale ru").Should().BeTrue();

        seen.Should().Equal("list", "locale ru");
    }

    [Fact]
    public void ConsoleService_Execute_ALineThatStopsAboveALeaf_RunsTheGroupThatHoldsACommand()
    {
        var console = new ConsoleService();
        var ran = string.Empty;

        console.Register("cvars", "Lists the settings.", _ => ran = "cvars");
        console.Register("cvars set", "Writes a setting.", _ => ran = "set");

        // 'cvars set locale' reaches the leaf 'cvars set' with the argument 'locale', and a word below a leaf is an argument of it
        // rather than a level: the leaf that ran is the deepest one the words walked to.
        console.Execute("cvars set locale").Should().BeTrue();

        ran.Should().Be("set");
    }

    [Fact]
    public void ConsoleService_Complete_CompletesALevelBehindAPathOneWordAtATime()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Writes a setting.", _ => { });
        console.Register("cvars get", "Answers a setting.", _ => { });

        // A word that is already a whole level is left alone, so 'cvars' is not shortened to itself and the space that descends the
        // tree is typed. The word behind the path is completed to the level the path leads to.
        console.SetInput("cvars");
        console.Complete().Should().BeFalse("the first level is already the whole word it could be");

        console.SetInput("cvars se");
        console.Complete().Should().BeTrue();
        console.Input.Should().Be("cvars set", "the level behind the path is completed and the path is left in place");
    }

    [Fact]
    public void ConsoleService_Complete_OfAWholeLeaf_LeavesTheLineAlone()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Writes a setting.", _ => { });

        foreach (char character in "cvars set")
        {
            console.Type(character);
        }

        console.Complete().Should().BeFalse("a word that is already the level it matches is nothing to complete");
    }

    [Fact]
    public void ConsoleService_Complete_ReplacesOnlyTheLastWordAndKeepsThePath()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Writes a setting.", _ => { });

        // A setting that was named is an argument of the level above it, and the word that is completed is the level, so the words
        // that are already typed stay exactly as they were and only the last one changes.
        console.SetInput("cvars se");
        console.Complete().Should().BeTrue();

        console.Input.Should().Be("cvars set", "the path that was typed is kept and only the last word is completed");
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
    public void ConsoleService_Complete_CompletesTheLastWordBehindAPath()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Sets a setting.", _ => { });

        foreach (char character in "cvars se")
        {
            console.Type(character);
        }

        console.Complete().Should().BeTrue("the last word is completed and the path that is already typed stays");
        console.Input.Should().Be("cvars set");
    }

    [Fact]
    public void ConsoleService_Complete_CompletesBehindAPathTypedWithExtraWhitespace()
    {
        var console = new ConsoleService();
        console.Register("cvars set", "Sets a setting.", _ => { });

        foreach (char character in "cvars\t se")
        {
            console.Type(character);
        }

        console.Complete().Should().BeTrue("a path typed with a tab still names the command it completes");
        console.Input.Should().Be("cvars set", "the path is written back with the single spaces a name holds");
    }

    [Fact]
    public void ConsoleService_Complete_OfAWordThatMatchesNothing_LeavesTheLineAlone()
    {
        var console = new ConsoleService();
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        foreach (char character in "spawn zzz")
        {
            console.Type(character);
        }

        console.Complete().Should().BeFalse("no command answers the word, so there is nothing to complete");
        console.Input.Should().Be("spawn zzz");
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
