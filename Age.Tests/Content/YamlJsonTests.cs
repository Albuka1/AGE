using System.Text.Json;
using Age.Content;
using Age.Content.Yaml;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class YamlJsonTests
{
    [Fact]
    public void YamlJson_Write_TurnsTheShapesOfADocumentIntoJson()
    {
        const string Text = """
            name: Sword
            flag: true
            whole: 12
            fraction: 3.5
            nothing: ~
            quiet: "true"
            nested:
              X: 1
              Y: 2
            list:
              - a
              - b
            """;

        JsonElement values = YamlJson.Write(YamlReader.Read(Text, "sword.yml"));

        values.GetProperty("name").GetString().Should().Be("Sword");
        values.GetProperty("flag").GetBoolean().Should().BeTrue();
        values.GetProperty("whole").GetInt32().Should().Be(12);
        values.GetProperty("fraction").GetDouble().Should().Be(3.5);
        values.GetProperty("nothing").ValueKind.Should().Be(JsonValueKind.Null);
        values.GetProperty("quiet").ValueKind.Should().Be(JsonValueKind.String, "a word in quotes stays a word");
        values.GetProperty("nested").GetProperty("Y").GetInt32().Should().Be(2);
        values.GetProperty("list").EnumerateArray().Select(item => item.GetString()).Should().Equal("a", "b");
    }

    [Fact]
    public void YamlJson_Write_KeepsAValueThatIsNotANumberAWord()
    {
        const string Text = """
            notANumber: NaN
            infinite: Infinity
            negative: -Infinity
            tooLarge: 1e999
            """;

        JsonElement values = YamlJson.Write(YamlReader.Read(Text, "numbers.yml"));

        foreach (string name in new[] { "notANumber", "infinite", "negative", "tooLarge" })
        {
            values.GetProperty(name).ValueKind.Should().Be(JsonValueKind.String, "JSON cannot hold {0} as a number", name);
        }

        values.GetProperty("tooLarge").GetString().Should().Be("1e999");
    }
}
