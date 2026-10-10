using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what a game declares about the engine it was written for to what the engine does with it: a version of another major number
/// is refused, an older one of the same major number is run, and a suffix describes the build rather than the contract.
/// </summary>
public sealed class VersionCheckTests
{
    [Theory]
    [InlineData("0.4.0", "0.4.0")]
    [InlineData("0.4.0", "0.4")]
    [InlineData("0.4.0", "0.4.0-alpha.1")]
    [InlineData("0.4.5", "0.4.0")]
    public void VersionCheck_ASameMajorAndAnySuffixOrLaterPatch_IsRun(string engine, string declared)
    {
        VersionCheck.Compare(engine, declared).Should().NotBe(VersionMatch.Different, "a change of the minor or the patch number is not a change of the contract");
    }

    [Fact]
    public void VersionCheck_AnotherMajorNumber_IsNotRun()
    {
        VersionCheck.Compare("0.4.0", "0.5.0").Should().Be(VersionMatch.Different, "before 1.0 the minor number carries a change a game has to follow, so 0.4 and 0.5 are not the same contract");
        VersionCheck.Compare("1.0.0", "0.4.0").Should().Be(VersionMatch.Different);
        VersionCheck.Compare("0.4.0", "1.0.0").Should().Be(VersionMatch.Different);
        VersionCheck.Compare("1.4.0", "2.0.0").Should().Be(VersionMatch.Different);
    }

    [Fact]
    public void VersionCheck_TheSameVersion_IsTheSameAndAnotherOfTheSameMajorIsCompatible()
    {
        VersionCheck.Compare("0.4.0", "0.4.0").Should().Be(VersionMatch.Same);
        VersionCheck.Compare("0.4.1", "0.4.0").Should().Be(VersionMatch.Compatible, "another minor or patch of the same major number is a build that moved, which a person reads rather than a game that will not start");
        VersionCheck.Compare("0.4.0", "0.4.9").Should().Be(VersionMatch.Compatible);
    }

    [Fact]
    public void VersionCheck_AGameThatShipsNoFile_DeclaresNothing()
    {
        string folder = Path.Combine(Path.GetTempPath(), "age-version-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(folder);

            VersionCheck.Read(folder).Should().BeNull("a game that ships no declaration is one that says nothing about the engine");

            File.WriteAllText(Path.Combine(folder, GameVersion.FileName), """{ "engine": "0.4.0" }""");

            VersionCheck.Read(folder).Should().NotBeNull();
            VersionCheck.Read(folder)!.Engine.Should().Be("0.4.0");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("0.4.x")]
    [InlineData("0.4.-1")]
    [InlineData("0.4.0.5")]
    [InlineData("1..0")]
    [InlineData("")]
    public void VersionCheck_AVersionThatDoesNotRead_IsRefusedRatherThanRounded(string version)
    {
        // A version that does not read is refused: reading `0.4.x` as `0.4.0` would run a game of another contract, and a fourth
        // number would be dropped without a word. The engine's own version and the declared one are read the same way.
        Action compare = () => VersionCheck.Compare("0.4.0", version);

        compare.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VersionCheck_AGameThatDeclaresNothingInAFile_IsRefused()
    {
        string folder = Path.Combine(Path.GetTempPath(), "age-version-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(folder);

            // A missing file is a game that declares nothing, which is accepted; a file that holds `null` is a game that wrote a
            // declaration and said nothing in it, which is a mistake rather than the same thing.
            File.WriteAllText(Path.Combine(folder, GameVersion.FileName), "null");

            Action read = () => VersionCheck.Read(folder);

            read.Should().Throw<InvalidDataException>("a declaration that is there and holds nothing is not a declaration that was never written");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void EngineVersion_ReportsThreeNumbers()
    {
        EngineVersion.Value.Should().MatchRegex(@"^\d+\.\d+\.\d+$", "the version of the engine is three numbers, which is what a game declares");
    }
}
