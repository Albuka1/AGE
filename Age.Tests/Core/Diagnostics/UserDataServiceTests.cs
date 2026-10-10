using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the folder of a game that belongs to the person playing it to what a game asks of it: the folders are named after the game
/// under the application data of the account, they are made when they are asked for, and a name of a file is a name below the folder
/// rather than a way out of it.
/// </summary>
public sealed class UserDataServiceTests : IDisposable
{
    private readonly string _roaming = Path.Combine(Path.GetTempPath(), "age-userdata-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_roaming))
        {
            Directory.Delete(_roaming, recursive: true);
        }
    }

    [Fact]
    public void UserDataService_Folders_AreNamedAfterTheGameUnderTheAccountData()
    {
        var data = new UserDataService("AGE Sample", _roaming);

        data.Game.Should().Be("AGE Sample");
        data.Root.Should().Be(Path.Combine(_roaming, "AGE Sample"));
        data.Data.Should().Be(Path.Combine(data.Root, "data"));
        data.Saves.Should().Be(Path.Combine(data.Root, "data", "saves"));
        data.Screenshots.Should().Be(Path.Combine(data.Root, "data", "screenshots"));
    }

    [Fact]
    public void UserDataService_ANameThatClimbsOutOfTheFolderOfTheGame_IsRefused()
    {
        var data = new UserDataService("AGE Sample", _roaming);

        Action save = () => data.PathIn(UserDataFolder.Saves, "../settings.json");
        Action absolute = () => data.PathIn(UserDataFolder.Saves, Path.Combine(_roaming, "elsewhere.json"));

        save.Should().Throw<ArgumentException>("a name of a file is a name below the folder of the game and nowhere else");
        absolute.Should().Throw<ArgumentException>("an absolute path leaves the folder of the game as well");
    }

    [Fact]
    public void UserDataService_AFolder_IsMadeWhenItIsAskedForAndKeptThereafter()
    {
        var data = new UserDataService("AGE Sample", _roaming);

        // A first run that writes nothing leaves nothing behind, so the folders are made here and nowhere else.
        Directory.Exists(data.Saves).Should().BeFalse("nothing was asked for yet");

        string save = data.PathIn(UserDataFolder.Saves, "campaign.json");

        Directory.Exists(data.Saves).Should().BeTrue("the folder is made when a file in it is asked for");
        Path.GetDirectoryName(save).Should().Be(data.Saves);
        File.Exists(save).Should().BeFalse("the file itself is the business of the game that writes it");

        data.Ensure(UserDataFolder.Screenshots).Should().Be(data.Screenshots);
        Directory.Exists(data.Screenshots).Should().BeTrue();
    }

    [Fact]
    public void UserDataService_ARelativeFolder_StillHoldsTheNamesOfTheGameAndRefusesOneThatClimbsOut()
    {
        // A folder that was given as a relative path is compared in the same absolute form as the name that is joined to it. The
        // folder of the test is under the one the process runs in, so the relative path is a real one whatever drive the temporary
        // folder is on: a name below the game folder is accepted rather than refused, and one that climbs out is still refused.
        string game = "age-userdata-" + Guid.NewGuid().ToString("N");
        string under = Path.Combine(Directory.GetCurrentDirectory(), game);
        string roaming = Path.GetRelativePath(Directory.GetCurrentDirectory(), under);

        try
        {
            var data = new UserDataService("AGE Sample", roaming);
            string save = data.PathIn(UserDataFolder.Saves, "campaign.json");

            save.Should().StartWith(Path.GetFullPath(under), "a name below the folder of the game is accepted");

            Action climbing = () => data.PathIn(UserDataFolder.Saves, "../settings.json");

            climbing.Should().Throw<ArgumentException>("a name that climbs out of the folder is refused whatever form the folder was given in");
        }
        finally
        {
            if (Directory.Exists(under))
            {
                Directory.Delete(under, recursive: true);
            }
        }
    }

    [Fact]
    public void UserDataService_ANameOfTheGameThatIsAFolderName_IsTheOnlyThingAccepted()
    {
        Action empty = () => new UserDataService("  ", _roaming);
        Action separator = () => new UserDataService("Age/Sample", _roaming);

        empty.Should().Throw<ArgumentException>();
        separator.Should().Throw<ArgumentException>("the name of the game is a folder name, so it holds no path separator");
    }
}
