using System.Text.Json;
using System.Text.Json.Serialization;

namespace Age.Core;

/// <summary>
/// Writes an <see cref="EntityRef"/> as the plain number of the scene identifier it holds, and reads that number back.
/// </summary>
/// <remarks>
/// The converter is public because a game compiles its own source generated context for its components, and the code
/// that the generator writes has to reach the converter from another assembly.
/// </remarks>
public sealed class EntityRefJsonConverter : JsonConverter<EntityRef>
{
    /// <inheritdoc />
    /// <exception cref="JsonException">The value is not a number.</exception>
    public override EntityRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number
            ? new EntityRef(reader.GetInt32())
            : throw new JsonException($"An entity reference is the number of the entity in its scene, not {reader.TokenType}.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, EntityRef value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.SceneId);
}
