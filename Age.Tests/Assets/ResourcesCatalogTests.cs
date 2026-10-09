using Age.Assets;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ResourcesCatalogTests
{
    [Fact]
    public void ResourcesCatalog_EveryFileTheRepositoryShips_IsNamedInTheReadme()
    {
        var loader = new NullAssetLoader();
        loader.Initialize(Root);
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        IEnumerable<string> files = loader.Enumerate(".");

        files.Should().NotBeEmpty("the repository ships assets");
        files.Should().OnlyContain(
            file => readme.Contains(Path.GetFileName(file), StringComparison.Ordinal),
            "a file that the catalogue does not name is a file a person cannot find: add a line to Resources/README.md");
    }

    [Fact]
    public void ResourcesCatalog_EveryPathTheReadmeNames_IsInTheFolder()
    {
        var loader = new NullAssetLoader();
        loader.Initialize(Root);
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        List<string> named = Paths(readme);

        named.Should().NotBeEmpty("the catalogue names the files a project loads by path");
        named.Should().OnlyContain(path => loader.Exists(path), "a path the catalogue names has to be a path a game can load");
    }

    /// <summary>Returns the paths that the catalogue writes between backticks, which is how it names a file of a project.</summary>
    private static List<string> Paths(string readme)
    {
        var paths = new List<string>();
        int index = readme.IndexOf('`');

        while (index >= 0)
        {
            int end = readme.IndexOf('`', index + 1);

            if (end < 0)
            {
                break;
            }

            string token = readme[(index + 1)..end];

            // A path of this folder is a section, a file and a separator between them: a name of a type or of a property,
            // such as Directory.Build.props, has no separator and is not one.
            if (token.Contains('/', StringComparison.Ordinal) && token.Contains('.', StringComparison.Ordinal))
            {
                paths.Add(token);
            }

            index = readme.IndexOf('`', end + 1);
        }

        return paths;
    }

    /// <summary>Gets the folder of the repository that the tests read, which is copied next to their output.</summary>
    private static string Root => Path.Combine(AppContext.BaseDirectory, "Resources");
}
