using Age.Assets;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ResourcesCatalogTests
{
    [Fact]
    public void ResourcesCatalog_EveryFileTheRepositoryShips_IsNamedByTheTable()
    {
        var loader = new NullAssetLoader();
        loader.Initialize(Root);
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        // The catalogue itself is not a file of a folder of the catalogue, so it is the one file that no row names.
        List<string> files = [.. loader.Enumerate(".").Where(file => file != "README.md")];
        List<string> catalogue = Catalogue(readme);

        files.Should().NotBeEmpty("the repository ships assets");
        catalogue.Should().NotBeEmpty("the table of the catalogue names a folder and the files in it");
        files.Should().OnlyContain(file => catalogue.Contains(file), "a file that no row of the catalogue names is a file a person cannot find: add it to the table of Resources/README.md");
        catalogue.Should().OnlyContain(path => loader.Exists(path), "a file a row names has to be in the folder: take it out of Resources/README.md when it goes away");
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
    /// <remarks>A path of this folder is a section, a file and a separator between them: a name of a type or of a property, such as Directory.Build.props, has no separator and is not one.</remarks>
    private static List<string> Paths(string readme) =>
        [.. Tokens(readme).Where(token => token.Contains('/', StringComparison.Ordinal) && token.Contains('.', StringComparison.Ordinal))];

    /// <summary>Returns the paths that the table of the catalogue names, by the folder of a row and the files that the row lists.</summary>
    /// <remarks>
    /// A row is <c>| folder | contents | used by |</c>, and the files of the folder are the names in backticks that hold a
    /// dot and no separator: a row that names <c>Textures/Tiles</c> and <c>tiles.bmp</c> stands for
    /// <c>Textures/Tiles/tiles.bmp</c>, which is the path a game loads and the path the loader hands out.
    /// </remarks>
    private static List<string> Catalogue(string readme)
    {
        var paths = new List<string>();

        foreach (string line in readme.Split('\n'))
        {
            string[] cells = line.Trim().Split('|');

            // A line that is not a row of the table, and the row that separates the header from it, name no folder.
            if (cells.Length < 4 || Tokens(cells[1]) is not [string folder, ..])
            {
                continue;
            }

            // Only the column that says what the folder holds: the column that says who uses it names assemblies, and an
            // assembly name holds a dot too.
            foreach (string token in Tokens(cells[2]))
            {
                if (token.Contains('.', StringComparison.Ordinal) && !token.Contains('/', StringComparison.Ordinal))
                {
                    paths.Add($"{folder}/{token}");
                }
            }
        }

        return paths;
    }

    /// <summary>Returns what a text writes between backticks.</summary>
    private static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        int index = text.IndexOf('`');

        while (index >= 0)
        {
            int end = text.IndexOf('`', index + 1);

            if (end < 0)
            {
                break;
            }

            tokens.Add(text[(index + 1)..end]);
            index = text.IndexOf('`', end + 1);
        }

        return tokens;
    }

    /// <summary>Gets the folder of the repository that the tests read, which is copied next to their output.</summary>
    private static string Root => Path.Combine(AppContext.BaseDirectory, "Resources");
}
