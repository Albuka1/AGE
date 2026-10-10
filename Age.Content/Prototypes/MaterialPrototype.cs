using System.Text.Json;
using System.Text.Json.Nodes;
using Age.Core;

namespace Age.Content.Prototypes;

/// <summary>
/// The data of a material of a content: what a document of the kind <see cref="Kind"/> declares, read as what a layer of a sprite
/// is drawn with.
/// </summary>
/// <remarks>
/// <para>
/// A material is the one place that holds the stages of a program and the values its uniforms start with, which is why a sprite
/// names it rather than the path of a stage: a hundred entities that share a program are a hundred references to one document
/// rather than a hundred paths, and a change of the program is a change in one file.
/// </para>
/// <para>
/// The values are read here rather than in a frame, and they are read with reflection, because a field of a struct says what a
/// value is while a document only writes text. The registry that a game already fills for its components is what reads them, so a
/// material, a scene and a prototype describe a value the same way and <c>Age.Content.Lint</c> can refuse a value without a device
/// at all. A material that is malformed is a mistake of the content, so it is refused with a <see cref="PrototypeException"/> that
/// names the file and the line rather than left for the first frame.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// - type: material
///   id: Pulse
///   fragment: Shaders/pulse.frag
///   uniforms:
///     - name: speed
///       type: float
///       value: 4.0
/// </code>
/// </example>
public sealed class MaterialPrototype : IPrototype
{
    /// <summary>The word that a document writes in its <c>type</c> field to declare a material.</summary>
    public const string Kind = "material";

    /// <summary>Initializes the data of a material.</summary>
    /// <param name="id">The identifier the document declared.</param>
    /// <param name="fragment">The path of the fragment stage.</param>
    /// <param name="vertex">The path of the vertex stage, or null for the stage of the engine.</param>
    /// <param name="values">The values of the uniforms as a set of names and values.</param>
    /// <param name="file">The file the document came from.</param>
    /// <param name="line">The line of the file, counting from one.</param>
    internal MaterialPrototype(string id, string fragment, string? vertex, JsonElement values, string file, int line)
    {
        Id = id;
        Fragment = fragment;
        Vertex = vertex;
        Values = values;
        File = file;
        Line = line;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>Gets the path of the fragment stage of the material, relative to the game root.</summary>
    public string Fragment { get; }

    /// <summary>Gets the path of the vertex stage of the material, or null when the material draws with the stage of the engine.</summary>
    public string? Vertex { get; }

    /// <summary>Gets the values of the uniforms of the material as a set of names and values, which is what <see cref="Read{T}"/> reads.</summary>
    public JsonElement Values { get; }

    /// <summary>Gets the file the material was read from, which an error mentions.</summary>
    public string File { get; }

    /// <summary>Gets the line of the file that declared the material, counting from one.</summary>
    public int Line { get; }

    /// <inheritdoc />
    public override string ToString() => $"material '{Id}'";

    /// <summary>Reads the values of the uniforms of a material as the struct that a game draws with.</summary>
    /// <typeparam name="T">The struct that holds the uniforms, which is a type of a game or of the engine.</typeparam>
    /// <param name="components">The registry that reads a value, which is the one a game filled for its components.</param>
    /// <returns>The values, with what the fields of <typeparamref name="T"/> start with for everything the document did not write.</returns>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    /// <remarks>A game calls this once, at startup, and keeps what it read; the renderer of the engine reads the same values when a layer of a prototype names a material, which is what keeps one material drawing the same thing whoever reads it.</remarks>
    public T Read<T>(ComponentRegistry components) where T : struct
    {
        ArgumentNullException.ThrowIfNull(components);

        return UniformReader.Read<T>(components, Values, $"the material '{Id}'");
    }

    /// <summary>Reads the values of a material, which is what a game registers as the kind <see cref="Kind"/>.</summary>
    /// <param name="data">The prototype that a document declared.</param>
    /// <returns>The data of the material.</returns>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="PrototypeException">The document does not say which stage draws the material, or a value of it is not one that its type can hold.</exception>
    public static MaterialPrototype Read(Prototype data)
    {
        ArgumentNullException.ThrowIfNull(data);

        string? fragment = null;
        string? vertex = null;
        JsonElement values = default;

        foreach (PrototypeComponent field in data.Fields)
        {
            switch (field.Name.ToLowerInvariant())
            {
                case "fragment":
                case "shader":
                    fragment ??= Text(data, field);
                    break;

                case "vertex":
                    vertex ??= Text(data, field);
                    break;

                case "uniforms":
                    if (field.Values.ValueKind != JsonValueKind.Object)
                    {
                        throw new PrototypeException(
                            $"{field.File}: the values of the material '{data.Id}' are a set of names and values, and they are {field.Values.ValueKind}",
                            field.File,
                            field.Line);
                    }

                    values = field.Values;
                    break;

                default:
                    // The fields of a material are written the way a document writes its own, so a name this does not read is
                    // one that a person meant to be read: refusing it keeps a typo from becoming a material that draws
                    // something other than what its document says.
                    throw new PrototypeException(
                        $"{field.File}: '{field.Name}' is not a field of a material, and a material says which stage draws it with 'fragment', an optional 'vertex', and the values of its uniforms with 'uniforms'",
                        field.File,
                        field.Line);
            }
        }

        if (fragment is null)
        {
            throw new PrototypeException(
                $"{data.File}: the material '{data.Id}' does not say which stage draws it, and a material names one with 'fragment'",
                data.File,
                data.Line);
        }

        if (values.ValueKind == JsonValueKind.Undefined)
        {
            values = JsonSerializer.SerializeToElement(new JsonObject());
        }

        return new MaterialPrototype(data.Id, fragment, vertex, values, data.File, data.Line);
    }

    /// <summary>Returns the text that a field of a material holds, which is the path of a stage.</summary>
    private static string Text(Prototype data, PrototypeComponent field) =>
        field.Values.ValueKind == JsonValueKind.String && field.Values.GetString() is { Length: > 0 } text
            ? text
            : throw new PrototypeException(
                $"{field.File}: the '{field.Name}' of the material '{data.Id}' is the path of a stage, and this one holds {field.Values.ValueKind}",
                field.File,
                field.Line);
}
