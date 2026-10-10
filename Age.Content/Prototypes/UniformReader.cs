using System.Reflection;
using System.Text.Json;
using Age.Core;

namespace Age.Content.Prototypes;

/// <summary>
/// Reads the named values of a material — or of any other document — into a struct, through the registry that reads the components
/// of a game.
/// </summary>
/// <remarks>
/// <para>
/// A value of a document is text, and the field that holds it is what says what the text means: a number that a field holds as a
/// <c>float</c> is read as one, and the same number behind an <c>int</c> is read as a whole number. The reader therefore needs the
/// type of the struct, which is why it is chosen where the values are used rather than where the document is read, and it reads
/// through <see cref="ComponentRegistry"/> so a value of a material is read by the same code as a value of a scene.
/// </para>
/// <para>
/// A value is written as a set of one name and the numbers behind it — <c>float: 4.0</c>, <c>color: 255, 128, 0, 255</c> — which
/// is how the name of a type travels with a value that a document writes as text. A name that the struct does not declare, and a
/// value that the field behind it cannot hold, are mistakes of the content: they are refused once, where the material is read to
/// be registered, rather than on every frame that draws it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// struct PulseUniforms
/// {
///     public float Speed;
///     public Color Tint;
/// }
///
/// PulseUniforms uniforms = UniformReader.Read&lt;PulseUniforms&gt;(components, values, "Pulse");
/// </code>
/// </example>
public static class UniformReader
{
    /// <summary>Reads the values of a document as the struct that holds them.</summary>
    /// <typeparam name="T">The struct that holds the values, which is a type of a game or of the engine.</typeparam>
    /// <param name="components">The registry that reads one value, which is the one a game filled for its components.</param>
    /// <param name="values">The values, by the name of the field that holds each of them.</param>
    /// <param name="what">What the values belong to, such as the identifier of a material, which a refusal mentions.</param>
    /// <returns>The values, with what the fields of <typeparamref name="T"/> start with for everything the document did not write.</returns>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    /// <exception cref="InvalidOperationException">A value is not one that the field behind it can hold.</exception>
    public static T Read<T>(ComponentRegistry components, JsonElement values, string what)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(components);

        object read = new T();
        Read(components, typeof(T), values, ref read, what);
        return (T)read;
    }

    /// <summary>Reads the values of a document into a boxed struct, one level at a time.</summary>
    /// <param name="components">The registry that reads one value.</param>
    /// <param name="type">The struct that holds the values.</param>
    /// <param name="values">The values, by the name of the field that holds each of them.</param>
    /// <param name="target">The struct that is filled.</param>
    /// <param name="what">What the values belong to, which a refusal mentions.</param>
    private static void Read(ComponentRegistry components, Type type, JsonElement values, ref object target, string what)
    {
        if (values.ValueKind == JsonValueKind.Null || values.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        if (values.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The values of {what} are a set of names and values, and they are a {values.ValueKind}.");
        }

        foreach (JsonProperty uniform in values.EnumerateObject())
        {
            if (Member(type, uniform.Name) is not (MemberInfo member, Type memberType))
            {
                throw new InvalidOperationException($"The values of {what} hold '{uniform.Name}', and {type.Name} declares no field of that name.");
            }

            if (memberType.Namespace is string space && space.StartsWith("Age.", StringComparison.Ordinal) && uniform.Value.ValueKind == JsonValueKind.Object && uniform.Value.EnumerateObject().All(field => Member(memberType, field.Name) is not null))
            {
                // A value whose names are the members of a struct is the struct itself, which is read one level down rather than
                // as a value that says what type it is: a vector is written as the fields of a vector by a scene and by a
                // prototype alike, and a material reads it the same way.
                object nested = ValueOf(member, target) ?? Activator.CreateInstance(member.Type())!;
                Read(components, memberType, uniform.Value, ref nested, what);
                member.SetValue(target, nested);
                continue;
            }

            member.SetValue(target, Value(components, memberType, uniform.Name, uniform.Value, what));
        }
    }

    /// <summary>Reads one value of a document into what the field behind it holds.</summary>
    /// <param name="components">The registry that reads one value.</param>
    /// <param name="type">The type of the field that holds the value.</param>
    /// <param name="name">The name of the value, which a refusal mentions.</param>
    /// <param name="value">The value to read.</param>
    /// <param name="what">What the values belong to, which a refusal mentions.</param>
    /// <returns>The value that was read, as the type of the field.</returns>
    /// <exception cref="InvalidOperationException">The value is not one that the field can hold.</exception>
    /// <remarks>
    /// A value is read through the registry of the components of a game, which is what makes this the same read that a scene makes:
    /// a type that a game registered with the contract of its component is written the way that component is written. A set of one
    /// name is a value whose name says what type it is, and it is unwrapped here before it reaches the contract.
    /// </remarks>
    private static object? Value(ComponentRegistry components, Type type, string name, JsonElement value, string what)
    {
        JsonElement plain = Typed(value);
        Type contract = type.IsEnum ? typeof(string) : type;

        try
        {
            if (components.TryDeserialize(Name(components, contract), plain, out object? read) && read is not null)
            {
                return read;
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The value '{name}' of {what} is not one that {type.Name} holds: {exception.Message}", exception);
        }

        // A type that no component carries is read by the contract of JSON itself, which is what a game that wants a plain
        // struct of numbers, boxes and words rather than a component of the engine does.
        try
        {
            return plain.Deserialize(contract);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The value '{name}' of {what} is not one that {type.Name} holds: {exception.Message}", exception);
        }
    }

    /// <summary>Returns the value behind the name of the type it says it is, which is how a document writes one number.</summary>
    /// <remarks>A value that is already the number of a vector is left alone: the name of a type is what a document writes to say what a value is, and a value that writes none is the value itself.</remarks>
    private static JsonElement Typed(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return value;
        }

        foreach (JsonProperty field in value.EnumerateObject())
        {
            // A set of one name is a value that says what type it is; a set of more names is the members of a struct.
            if (value.EnumerateObject().Count() == 1)
            {
                return field.Value;
            }

            return value;
        }

        return value;
    }

    /// <summary>Returns the name that a type is registered under, or the name of the type itself when it is registered under none.</summary>
    private static string Name(ComponentRegistry components, Type type) =>
        components.Names.FirstOrDefault(name => components.TryGetType(name, out Type? registered) && registered == type) ?? type.Name;

    /// <summary>Returns the member that holds a value and the type of what it holds, or null when the type declares none.</summary>
    private static (MemberInfo Member, Type Type)? Member(Type type, string name)
    {
        foreach (MemberInfo member in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
            {
                return (member, ((FieldInfo)member).FieldType);
            }
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (string.Equals(property.Name, name, StringComparison.Ordinal) && property.SetMethod?.IsPublic is true && property.GetIndexParameters().Length == 0)
            {
                return (property, property.PropertyType);
            }
        }

        return null;
    }

    /// <summary>Returns the value that a member of a struct holds, or null when the member is not one of the two kinds a document writes.</summary>
    private static object? ValueOf(MemberInfo member, object target) => member switch
    {
        FieldInfo field => field.GetValue(target),
        PropertyInfo property => property.GetValue(target),
        _ => null,
    };

    /// <summary>Returns the type of a member of a struct.</summary>
    private static Type Type(this MemberInfo member) => member switch
    {
        FieldInfo field => field.FieldType,
        PropertyInfo property => property.PropertyType,
        _ => typeof(object),
    };

    /// <summary>Writes a value into a member of a struct.</summary>
    private static void SetValue(this MemberInfo member, object target, object? value)
    {
        if (member is FieldInfo field)
        {
            field.SetValue(target, value);
            return;
        }

        if (member is PropertyInfo property)
        {
            property.SetValue(target, value);
        }
    }
}
