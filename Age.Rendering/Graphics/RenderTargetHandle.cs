using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Identifies a surface that a renderer draws into instead of the window.
/// </summary>
/// <remarks>
/// A target is a texture with a framebuffer around it, so what a pass drew into it is an image afterwards: the handle carries
/// the texture, which a game binds as a sampler of its own or draws as a quad. Only the engine creates handles, because the
/// constructor is not public: a game asks the renderer for one with <see cref="IRenderer.CreateRenderTarget"/>.
/// </remarks>
public readonly struct RenderTargetHandle : IEquatable<RenderTargetHandle>
{
    /// <summary>Initializes a handle. Only the engine creates handles.</summary>
    /// <param name="framebuffer">The identifier of the framebuffer of the device.</param>
    /// <param name="texture">The identifier of the texture that holds what was drawn.</param>
    /// <param name="size">The size of the target, in pixels.</param>
    internal RenderTargetHandle(uint framebuffer, uint texture, Vector2 size)
    {
        Framebuffer = framebuffer;
        Texture = new TextureHandle((int)texture);
        Size = size;
    }

    /// <summary>Gets the identifier of the framebuffer that the device draws into.</summary>
    internal uint Framebuffer { get; }

    /// <summary>Gets the texture that holds what was drawn into the target.</summary>
    /// <remarks>It is a texture of the device like any other, so a game draws it with <see cref="IRenderer.DrawTextureRegion"/> or binds it with <see cref="IRenderer.SetSampler"/>. Its <see cref="TextureHandle.Resource"/> has no slot: the renderer owns it, not a texture service.</remarks>
    public TextureHandle Texture { get; }

    /// <summary>Gets the size of the target, in pixels.</summary>
    public Vector2 Size { get; }

    /// <summary>Determines whether two handles refer to the same target.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><see langword="true"/> when both refer to the same target.</returns>
    public static bool operator ==(RenderTargetHandle left, RenderTargetHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles refer to different targets.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><see langword="true"/> when they refer to different targets.</returns>
    public static bool operator !=(RenderTargetHandle left, RenderTargetHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    /// <param name="other">The handle to compare with.</param>
    /// <returns><see langword="true"/> when both refer to the same target.</returns>
    public bool Equals(RenderTargetHandle other) => Framebuffer == other.Framebuffer && Texture.Id == other.Texture.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RenderTargetHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Framebuffer, Texture.Id);
}
