using System.Globalization;
using Age.Content.Yaml;
using Age.Core;

namespace Age.Content.Sheets;

/// <summary>
/// Reads the document that describes a sprite sheet, which lives next to the image it slices.
/// </summary>
/// <remarks>
/// <para>
/// The document is written in the subset of YAML that the content of the engine uses, and it is a mapping:
/// </para>
/// <example>
/// <code>
/// version: 1
/// license: MIT
/// copyright: AGE, drawn for this repository
/// image: Textures/Entities/goblin.bmp
/// cell:
///   X: 16
///   Y: 16
/// columns: 4
/// rows: 3
/// states:
///   idle:
///     row: 0
///     frames: 4
///     delay: 0.15
///   attack:
///     row: 2
///     frames: 3
///     delay: 0.08
///     loop: false
/// </code>
/// </example>
/// <para>
/// <c>version</c> is required, and a document of a version this build does not read is refused. <c>license</c> and
/// <c>copyright</c> are what a game says about art that is not its own: the reader leaves them alone, and
/// <c>Age.Content.Lint</c> is what requires them of every sheet of a build. Every other field is required except
/// <c>delay</c>, which is a tenth of a second, and <c>loop</c>, which is true. A field that the document does not declare
/// and a value that does not fit are refused with the file and the line, so a sheet that is wrong is a message at the start
/// of a game rather than a frame that draws the wrong part of an image.
/// </para>
/// </remarks>
public static class SpriteSheetReader
{
    private const float DefaultDelay = 0.1f;

    /// <summary>Reads the document of a sprite sheet.</summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="file">The name of the file the text came from, which an error mentions.</param>
    /// <returns>The sheet that the document declares.</returns>
    /// <exception cref="ArgumentException">The file is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="SpriteSheetException">The document does not describe a sheet, and the error names the file and the line.</exception>
    public static SpriteSheet Read(string text, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(text);

        YamlValue document;

        try
        {
            document = YamlReader.Read(text, file);
        }
        catch (YamlException exception)
        {
            throw new SpriteSheetException(exception.Message, file, exception.Line);
        }

        if (document is not YamlMapping mapping)
        {
            throw new SpriteSheetException("a sprite sheet is a set of fields, and this document holds " + Shape(document), file, document.Line);
        }

        string? image = null;
        Vector2? cell = null;
        int? columns = null;
        int? rows = null;
        int? version = null;
        string? license = null;
        string? copyright = null;
        YamlEntry? states = null;

        foreach (YamlEntry entry in mapping.Entries)
        {
            switch (entry.Name)
            {
                case "version":
                    version = Positive(file, entry, "a version of the format");
                    break;

                case "image":
                    image = Word(file, entry);
                    break;

                case "cell":
                    cell = Cell(file, entry);
                    break;

                case "columns":
                    columns = Positive(file, entry, "a count of columns");
                    break;

                case "rows":
                    rows = Positive(file, entry, "a count of rows");
                    break;

                case "license":
                    license = Word(file, entry);
                    break;

                case "copyright":
                    copyright = Word(file, entry);
                    break;

                case "states":
                    states = entry;
                    break;

                default:
                    throw new SpriteSheetException($"{file}: '{entry.Name}' is not a field of a sprite sheet, and a sheet holds version, image, cell, columns, rows, license, copyright and states", file, entry.Line);
            }
        }

        if (version is not int declared)
        {
            throw new SpriteSheetException($"{file}: a sprite sheet says which version of the format it is written in with 'version', and this build reads version {SpriteSheet.CurrentVersion}", file, mapping.Line);
        }

        if (declared != SpriteSheet.CurrentVersion)
        {
            throw new SpriteSheetException(
                $"{file}: the sheet is written in version {declared}, and this build reads version {SpriteSheet.CurrentVersion}. Update the engine, or write the sheet again.",
                file,
                mapping.Line);
        }

        if (image is null)
        {
            throw new SpriteSheetException($"{file}: a sprite sheet names the image it slices with 'image'", file, mapping.Line);
        }

        if (cell is not Vector2 size)
        {
            throw new SpriteSheetException($"{file}: a sprite sheet says how big one frame is with 'cell'", file, mapping.Line);
        }

        if (columns is not int width || rows is not int height)
        {
            throw new SpriteSheetException($"{file}: a sprite sheet says how many cells its image holds with 'columns' and 'rows'", file, mapping.Line);
        }

        if (states is not YamlEntry statesEntry)
        {
            throw new SpriteSheetException($"{file}: a sprite sheet declares the states a game plays with 'states'", file, mapping.Line);
        }

        return new SpriteSheet(image, size, width, height, ReadStates(file, statesEntry, width, height), file, license, copyright);
    }

    /// <summary>Reads the states of a sheet, which are what a game plays.</summary>
    private static Dictionary<string, SpriteSheetState> ReadStates(string file, YamlEntry entry, int columns, int rows)
    {
        if (entry.Value is not YamlMapping mapping)
        {
            throw new SpriteSheetException($"{file}: the states of a sheet are a set of fields by name, and this one is {Shape(entry.Value)}", file, entry.Value.Line);
        }

        if (mapping.Entries.Count == 0)
        {
            throw new SpriteSheetException($"{file}: a sheet declares the states a game plays, and this one declares none", file, mapping.Line);
        }

        var states = new Dictionary<string, SpriteSheetState>(StringComparer.Ordinal);

        foreach (YamlEntry state in mapping.Entries)
        {
            if (state.Value is not YamlMapping fields)
            {
                throw new SpriteSheetException($"{file}: the state '{state.Name}' is a set of fields, and this one is {Shape(state.Value)}", file, state.Value.Line);
            }

            int? row = null;
            int? frames = null;
            float delay = DefaultDelay;
            bool loop = true;

            foreach (YamlEntry field in fields.Entries)
            {
                switch (field.Name)
                {
                    case "row":
                        row = Whole(file, field, "a row of the grid");
                        break;

                    case "frames":
                        frames = Positive(file, field, "a count of frames");
                        break;

                    case "delay":
                        delay = Number(file, field, "a length in seconds");
                        break;

                    case "loop":
                        loop = Flag(file, field);
                        break;

                    default:
                        throw new SpriteSheetException($"{file}: '{field.Name}' is not a field of the state '{state.Name}', and a state holds row, frames, delay and loop", file, field.Line);
                }
            }

            if (row is not int first || frames is not int count)
            {
                throw new SpriteSheetException($"{file}: the state '{state.Name}' says which row it lies on with 'row' and how many frames it holds with 'frames'", file, fields.Line);
            }

            if (first >= rows)
            {
                throw new SpriteSheetException($"{file}: the state '{state.Name}' lies on row {first}, and the sheet holds {rows} rows", file, fields.Line);
            }

            if (count > columns)
            {
                throw new SpriteSheetException($"{file}: the state '{state.Name}' holds {count} frames, and a row of the sheet holds {columns} cells", file, fields.Line);
            }

            states[state.Name] = new SpriteSheetState(state.Name, first, count, delay, loop);
        }

        return states;
    }

    /// <summary>Reads a field that holds one word.</summary>
    private static string Word(string file, YamlEntry entry) =>
        entry.Value is YamlScalar scalar && !scalar.IsEmpty
            ? scalar.Text
            : throw new SpriteSheetException($"{file}: the field '{entry.Name}' holds {Shape(entry.Value)} where one word is expected", file, entry.Value.Line);

    /// <summary>Reads a field that holds a whole number greater than zero.</summary>
    private static int Positive(string file, YamlEntry entry, string description) =>
        entry.Value is YamlScalar scalar
        && int.TryParse(scalar.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        && value > 0
            ? value
            : throw new SpriteSheetException($"{file}: the field '{entry.Name}' holds {Shape(entry.Value)} where {description} is a whole number greater than zero", file, entry.Value.Line);

    /// <summary>Reads a field that holds a whole number, which is what a position in a grid is: row zero is the first one.</summary>
    private static int Whole(string file, YamlEntry entry, string description) =>
        entry.Value is YamlScalar scalar
        && int.TryParse(scalar.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        && value >= 0
            ? value
            : throw new SpriteSheetException($"{file}: the field '{entry.Name}' holds {Shape(entry.Value)} where {description} is a whole number that is not negative", file, entry.Value.Line);

    /// <summary>Reads a field that holds a number greater than zero, which is what a length or a size is.</summary>
    private static float Number(string file, YamlEntry entry, string description) =>
        entry.Value is YamlScalar scalar
        && float.TryParse(scalar.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
        && float.IsFinite(value)
        && value > 0f
            ? value
            : throw new SpriteSheetException($"{file}: the field '{entry.Name}' holds {Shape(entry.Value)} where {description} is a number greater than zero", file, entry.Value.Line);

    /// <summary>Reads a field that holds a flag.</summary>
    private static bool Flag(string file, YamlEntry entry) => Word(file, entry) switch
    {
        "true" => true,
        "false" => false,
        var word => throw new SpriteSheetException($"{file}: the field '{entry.Name}' holds '{word}' where true or false is expected", file, entry.Value.Line),
    };

    /// <summary>Reads the size of one cell of the grid.</summary>
    private static Vector2 Cell(string file, YamlEntry entry)
    {
        if (entry.Value is not YamlMapping fields)
        {
            throw new SpriteSheetException($"{file}: the cell of a sheet is a set of fields, and this one is {Shape(entry.Value)}", file, entry.Value.Line);
        }

        float? x = null;
        float? y = null;

        foreach (YamlEntry side in fields.Entries)
        {
            switch (side.Name)
            {
                case "X":
                    x = Number(file, side, "a width");
                    break;

                case "Y":
                    y = Number(file, side, "a height");
                    break;

                default:
                    throw new SpriteSheetException($"{file}: '{side.Name}' is not a field of the cell of a sheet, and a cell holds X and Y", file, side.Line);
            }
        }

        return x is float width && y is float height
            ? new Vector2(width, height)
            : throw new SpriteSheetException($"{file}: the cell of a sheet says how big one frame is with 'X' and 'Y'", file, fields.Line);
    }

    /// <summary>Names the shape of a value, which is what a message about a document says about what it found.</summary>
    private static string Shape(YamlValue value) => value switch
    {
        YamlScalar => "a word",
        YamlSequence => "a list",
        YamlMapping => "a set of fields",
        _ => "something this reader does not know",
    };
}
