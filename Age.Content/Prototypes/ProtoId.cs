using System.Text.Json;
using System.Text.Json.Serialization;

namespace Age.Content.Prototypes;

/// <summary>
/// A reference to a prototype by its identifier, which a component can hold and a scene can save.
/// </summary>
/// <remarks>
/// <para>
/// The reference is written as a plain word, which is what makes a scene readable and what keeps a save small: a map that
/// says <c>0x5word</c> refers to the prototype of that name rather than carrying a copy of what it holds, so a change to
/// the prototype reaches every place that refers to it.
/// </para>
/// <para>
/// A reference is not a promise: the prototype behind it may be gone from the content of the game, which is why a
/// reference is resolved through <see cref="IPrototypeManager.TryGet{T}"/> rather than by a field access. A reference
/// that names nothing resolves to <see cref="None"/>, so a scene that lost the prototype it refers to is a scene that
/// reports it rather than one that reads the wrong data.
/// </para>
/// </remarks>
/// <typeparam name="T">The kind of the prototype, such as the data of an enemy.</typeparam>
/// <example>
/// <code>
/// public struct WeaponComponent : IComponent
/// {
///     public ProtoId&lt;WeaponPrototype&gt; Weapon;
/// }
/// </code>
/// </example>
[JsonConverter(typeof(ProtoIdJsonConverterFactory))]
public readonly struct ProtoId<T> : IEquatable<ProtoId<T>>
    where T : IPrototype
{
    /// <summary>Initializes a reference to a prototype by its identifier.</summary>
    /// <param name="id">The identifier the prototype was declared under.</param>
    /// <exception cref="ArgumentException">The identifier is null, empty or whitespace.</exception>
    public ProtoId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    /// <summary>Gets a reference that names no prototype.</summary>
    public static ProtoId<T> None => default;

    /// <summary>Gets the identifier of the prototype, which is null for <see cref="None"/>.</summary>
    public string? Id { get; }

    /// <summary>Gets a value indicating whether the reference names a prototype.</summary>
    public bool HasValue => !string.IsNullOrEmpty(Id);

    /// <summary>Determines whether two references name the same prototype.</summary>
    public static bool operator ==(ProtoId<T> left, ProtoId<T> right) => left.Equals(right);

    /// <summary>Determines whether two references name different prototypes.</summary>
    public static bool operator !=(ProtoId<T> left, ProtoId<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ProtoId<T> other) => string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ProtoId<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id is null ? 0 : StringComparer.Ordinal.GetHashCode(Id);

    /// <inheritdoc />
    public override string ToString() => Id ?? string.Empty;
}

/// <summary>
/// Writes a <see cref="ProtoId{T}"/> as the word it holds.
/// </summary>
/// <remarks>
/// The converter reaches the closed type of a reference through the reflection of the runtime, which an AOT build does not
/// have: such a build needs a converter of its own next to this one, which is part of the work of the AOT step rather
/// than of the content one.
/// </remarks>
public sealed class ProtoIdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ProtoId<>);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
    }

    /// <summary>Reads and writes the reference of one kind of prototype.</summary>
    private sealed class Converter<TPrototype> : JsonConverter<ProtoId<TPrototype>>
        where TPrototype : IPrototype
    {
        /// <inheritdoc />
        public override ProtoId<TPrototype> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null
                ? ProtoId<TPrototype>.None
                : new ProtoId<TPrototype>(reader.GetString() ?? throw new JsonException("A reference to a prototype is a word, and this one is null."));

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, ProtoId<TPrototype> value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (value.HasValue)
            {
                writer.WriteStringValue(value.Id);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
