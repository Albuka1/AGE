using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class CVarServiceTests
{
    [Fact]
    public void CVarService_RegisterAndGet_ReadTheValueOfASetting()
    {
        var cvars = new CVarService();

        cvars.Register("step", 1f / 60f, "The fixed step of the clock, in seconds.");
        cvars.Register("maxEntities", 4096, "The number of entities the world reserves room for.");
        cvars.Register("debug", false, "Draws the contacts of the frame.");

        cvars.Get<float>("step").Should().Be(1f / 60f);
        cvars.Get<int>("maxEntities").Should().Be(4096);
        cvars.Get<bool>("debug").Should().BeFalse();
        cvars.Values.Select(cvar => cvar.Name).Should().Equal("step", "maxEntities", "debug");
    }

    [Fact]
    public void CVarService_Register_RejectsANameTwiceAndOneThatHoldsWhitespace()
    {
        var cvars = new CVarService();
        cvars.Register("step", 1f, "The fixed step of the clock, in seconds.");

        Action twice = () => cvars.Register("step", 2f, "Another one.");
        Action spaced = () => cvars.Register("two words", 1, "Nothing.");

        twice.Should().Throw<ArgumentException>().WithMessage("*already registered*");
        spaced.Should().Throw<ArgumentException>().WithMessage("*whitespace*");
    }

    [Fact]
    public void CVarService_Get_ReportsASettingThatIsMissingOrOfAnotherType()
    {
        var cvars = new CVarService();
        cvars.Register("maxEntities", 4096, "The number of entities the world reserves room for.");

        Action missing = () => cvars.Get<int>("step");
        Action wrongType = () => cvars.Get<float>("maxEntities");

        missing.Should().Throw<InvalidOperationException>().WithMessage("*step*");
        wrongType.Should().Throw<InvalidOperationException>().WithMessage("*Single*");
        cvars.TryGet<int>("maxEntities", out int entities).Should().BeTrue();
        entities.Should().Be(4096);
        cvars.TryGet<float>("maxEntities", out _).Should().BeFalse();
    }

    [Fact]
    public void CVarService_SetFromText_ReadsTheValueWithTheInvariantCulture()
    {
        var cvars = new CVarService();
        cvars.Register("step", 1f / 60f, "The fixed step of the clock, in seconds.");

        cvars.SetFromText("step", "0.02").Should().BeTrue();

        cvars.Get<float>("step").Should().Be(0.02f, "a text that says 0.02 means the same thing everywhere");
    }

    [Fact]
    public void CVarService_SetFromText_ReportsWhatTheConsoleCannotWrite()
    {
        var console = new ConsoleService();
        var cvars = new CVarService(console);
        cvars.Register("maxEntities", 4096, "The number of entities the world reserves room for.");

        cvars.SetFromText("missing", "3").Should().BeFalse();
        cvars.SetFromText("maxEntities", "many").Should().BeFalse();

        console.Output.Should().HaveCount(2);
        console.Output[0].Should().StartWith("error:").And.Contain("missing");
        console.Output[1].Should().StartWith("error:").And.Contain("many");
        cvars.Get<int>("maxEntities").Should().Be(4096, "a text that does not fit the type leaves the setting as it was");
    }

    [Fact]
    public void CVarService_WithAConsole_RegistersACommandForEverySetting()
    {
        var console = new ConsoleService();
        var cvars = new CVarService(console);
        cvars.Register("step", 1f / 60f, "The fixed step of the clock, in seconds.");

        console.Execute("step").Should().BeTrue();
        int before = console.Output.Count;
        console.Output.Should().ContainSingle().Which.Should().StartWith("step = 0.0166", "an empty argument shows what the setting holds");

        console.Execute("step 0.5").Should().BeTrue();
        cvars.Get<float>("step").Should().Be(0.5f);

        console.Execute("cvars").Should().BeTrue();
        console.Output.Should().Contain(line => line.StartsWith("step = 0.5 - [Single] ", StringComparison.Ordinal));
    }

    [Fact]
    public void CVarService_Apply_WritesWhatAConfigurationSourceHolds()
    {
        var cvars = new CVarService();
        cvars.Register("step", 1f / 60f, "The fixed step of the clock, in seconds.");
        cvars.Register("debug", false, "Draws the contacts of the frame.");

        int written = cvars.Apply(new Dictionary<string, string>
        {
            ["step"] = "0.01",
            ["debug"] = "true",
            ["window.width"] = "1280",
            ["step.again"] = "0.5",
        });

        written.Should().Be(2, "a source holds more than the settings of the engine");
        cvars.Get<float>("step").Should().Be(0.01f);
        cvars.Get<bool>("debug").Should().BeTrue();
    }

    [Fact]
    public void CVarService_List_SaysWhatASettingHoldsAndWhatItIsFor()
    {
        var cvars = new CVarService();
        cvars.Register("maxEntities", 4096, "The number of entities the world reserves room for.");

        cvars.List().Should().Equal("maxEntities = 4096 - [Int32] The number of entities the world reserves room for.");

        List<CVar> values = [.. cvars.Values];
        values.Should().ContainSingle();
        values[0].Text.Should().Be("4096");
        values[0].Type.Should().Be(typeof(int));
    }
}
