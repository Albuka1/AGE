using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Identifies a shader that an <see cref="IShaderService"/> compiled from the content of a game.
/// </summary>
/// <remarks>
/// A handle records the slot of the service that compiled the shader, so a default value and a handle left behind when its
/// shader was unloaded both stop resolving instead of naming the program that took the slot. Only the engine creates handles,
/// because the constructor is not public: a game asks the service for one.
/// </remarks>
public readonly struct ShaderHandle : IEquatable<ShaderHandle>
{
    /// <summary>Initializes a handle. Only the engine creates handles.</summary>
    /// <param name="resource">The slot the shader occupies in the service that compiled it.</param>
    /// <param name="program">The identifier of the program of the device that the shader was compiled into.</param>
    /// <param name="generation">The attachment of the renderer that compiled the program, as <see cref="IRenderer.DeviceGeneration"/> reports it.</param>
    internal ShaderHandle(ResourceHandle resource, uint program, uint generation)
    {
        Resource = resource;
        Program = program;
        Generation = generation;
    }

    /// <summary>Gets the slot that the shader occupies in the service that compiled it.</summary>
    internal ResourceHandle Resource { get; }

    /// <summary>Gets the identifier of the program that the renderer draws with.</summary>
    internal uint Program { get; }

    /// <summary>Gets the attachment of the renderer that compiled the program, which is the number <see cref="IRenderer.DeviceGeneration"/> had at that moment.</summary>
    /// <remarks>
    /// A renderer that is attached to another window lets go of the device of the first one, so the program of the attachment before is gone, and the
    /// device that is there now hands the same numbers out again: the identifier of a program names two programs of two attachments. This is what tells
    /// the two apart, which is what keeps <see cref="IRenderer.UseShader"/> from drawing with whatever took the number.
    /// </remarks>
    public uint Generation { get; }

    /// <summary>Determines whether two handles refer to the same shader.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><see langword="true"/> when both refer to the same shader.</returns>
    public static bool operator ==(ShaderHandle left, ShaderHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles refer to different shaders.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><see langword="true"/> when they refer to different shaders.</returns>
    public static bool operator !=(ShaderHandle left, ShaderHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    /// <param name="other">The handle to compare with.</param>
    /// <returns><see langword="true"/> when both refer to the same shader.</returns>
    /// <remarks>Two handles are equal only when they were issued by one attachment of the renderer, because a device hands the number of a program out again.</remarks>
    public bool Equals(ShaderHandle other) => Program == other.Program && Resource == other.Resource && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ShaderHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Program, Resource, Generation);
}
