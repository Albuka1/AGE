using FluentAssertions;
using Xunit;

namespace Age.Tests.Content;

/// <summary>
/// Guards the stages of the content that the repository ships. A shader is compiled by the graphics device rather than by
/// anything that runs in a test, so this suite cannot compile one; what it can hold is the shape of the file, which is where the
/// mistake that broke a run of the sample came from: a line that opened with a hash, which the shading language reads as a
/// directive rather than as a comment, so the whole stage failed to compile as the game started.
/// </summary>
public sealed class ShaderContentTests
{
    /// <summary>The directives of the shading language, which are the only lines of a stage that may open with a hash.</summary>
    private static readonly string[] Directives =
    [
        "#version", "#define", "#undef", "#if", "#ifdef", "#ifndef", "#else", "#elif", "#endif", "#extension", "#line", "#pragma", "#error",
    ];

    private static string Root => Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");

    [Fact]
    public void Shaders_EveryStageOfTheContent_OpensWithADirectiveOnlyWhereTheLanguageHasOne()
    {
        var stages = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".frag" or ".vert")
            .ToList();

        stages.Should().NotBeEmpty("the sample draws a stage of the content, so the folder holds at least one of them");

        foreach (string stage in stages)
        {
            var line = 0;

            foreach (string text in File.ReadLines(stage))
            {
                line++;

                string trimmed = text.TrimStart();
                if (!trimmed.StartsWith('#'))
                {
                    continue;
                }

                string directive = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)[0];

                Directives.Should().Contain(directive, $"{Path.GetFileName(stage)} line {line} opens with a hash, which the shading language reads as a directive rather than as a comment: write // for the comment");
            }
        }
    }
}
