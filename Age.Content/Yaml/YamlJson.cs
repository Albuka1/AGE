using System.Text.Json;
using Age.Content.Yaml;

namespace Age.Content;

/// <summary>
/// Turns the tree of a YAML document into the JSON that the contracts of the components read.
/// </summary>
/// <remarks>
/// A prototype and a scene describe a component with one contract, and that contract is JSON: a document that a person
/// writes is YAML because it holds comments and reads like a list, and the values it holds are handed to the same reader
/// that a scene goes through. Nothing of the meaning is lost in between, because every name of a mapping is written the
/// way it was written and only the shape of the tree changes.
/// </remarks>
public static class YamlJson
{
    /// <summary>Writes a value of a document as JSON.</summary>
    /// <param name="value">The value to write.</param>
    /// <returns>The element that holds the same names and values.</returns>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="YamlException">The value holds something that a component contract cannot read.</exception>
    public static JsonElement Write(YamlValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, value);
        }

        buffer.Position = 0;
        using JsonDocument document = JsonDocument.Parse(buffer);
        return document.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, YamlValue value)
    {
        switch (value)
        {
            case YamlMapping mapping:
                writer.WriteStartObject();

                foreach (YamlEntry entry in mapping.Entries)
                {
                    writer.WritePropertyName(entry.Name);
                    Write(writer, entry.Value);
                }

                writer.WriteEndObject();
                return;

            case YamlSequence sequence:
                writer.WriteStartArray();

                foreach (YamlValue item in sequence.Items)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                return;

            case YamlScalar scalar:
                WriteScalar(writer, scalar);
                return;

            default:
                throw new YamlException($"the value of line {value.Line} is of a kind this writer does not know", value.Line, 1);
        }
    }

    /// <summary>Writes a word as the value it looks like, keeping a quoted word a word.</summary>
    private static void WriteScalar(Utf8JsonWriter writer, YamlScalar scalar)
    {
        string text = scalar.Text;

        if (!scalar.Quoted)
        {
            if (text.Length == 0 || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) || text == "~")
            {
                writer.WriteNullValue();
                return;
            }

            if (bool.TryParse(text, out bool flag))
            {
                writer.WriteBooleanValue(flag);
                return;
            }

            if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long whole))
            {
                writer.WriteNumberValue(whole);
                return;
            }

            // A word that parses as a number is written as one, which is what a component holds, but only while the number
            // is a number: NaN, an infinity and a value too large for a double are words that a writer of JSON refuses,
            // so they go on as the words they are rather than as a value that cannot be written.
            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
            {
                writer.WriteNumberValue(number);
                return;
            }
        }

        writer.WriteStringValue(text);
    }
}
