using Age.Core;

namespace Age.Physics;

/// <summary>
/// Defines the axis-aligned collision box of an entity, relative to its <see cref="TransformComponent"/>, and says what
/// the box is for: which layers it belongs to, which it answers, whether it blocks a body or only reports it, and whether
/// a resolution may move it.
/// </summary>
/// <remarks>
/// <para>
/// The box is built from the transform position, the offset and the size scaled by the transform scale. The rotation of
/// the transform is ignored: collision detection is axis-aligned only, so a rotated sprite still collides with an upright
/// box. Oriented boxes are on the roadmap.
/// </para>
/// <para>
/// A collider that is a <see cref="Trigger"/> is a zone rather than a body: it does not block a move and a resolution
/// never moves a body out of it, and the only thing it does is announce the pairs it takes part in. A door, a trap, a
/// place of rest and a body of a battle on a grid of cells are all triggers, because what a cell of the grid decides is
/// where a body stands and not what a box of it covers.
/// </para>
/// <para>
/// An <see cref="Immovable"/> collider is one that a resolution leaves where it stands, whatever it answers: a wall, a
/// chest, a piece of content that a fight does not push around. A pair of two immovable colliders is reported and never
/// resolved, because neither of them may move and moving both would be the same as moving one of them by the whole of the
/// penetration.
/// </para>
/// </remarks>
[Component("Collider")]
public struct ColliderComponent : IComponent
{
    /// <summary>Gets or sets the size of the box, before the transform scale is applied.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the offset from the transform position to the top-left corner of the box.</summary>
    public Vector2 Offset;

    /// <summary>Gets or sets the layers this collider belongs to, which the other collider of a pair reads its mask against.</summary>
    /// <remarks>
    /// A collider that leaves this at its default is in <see cref="CollisionLayer.Default"/> alone, so a game that never
    /// sets a layer has a physics that behaves as it did before the layers existed.
    /// </remarks>
    public CollisionLayer Layer;

    /// <summary>Gets or sets the layers this collider answers, which the layer of the other collider is tested against.</summary>
    /// <remarks>
    /// A collider that leaves this at its default answers <see cref="CollisionLayer.Everything"/>, which is what a game
    /// that names a layer for the two sides of a rule rather than a mask for each of them gets.
    /// </remarks>
    public CollisionLayer Mask;

    /// <summary>Gets or sets a value indicating whether the collider is a zone that reports a body rather than a body that blocks one.</summary>
    public bool Trigger;

    /// <summary>Gets or sets a value indicating whether a resolution may move the entity this collider stands on.</summary>
    public bool Immovable;

    /// <summary>Returns a collider of a size in one layer, which answers that layer alone.</summary>
    /// <param name="size">The size of the box, before the transform scale is applied.</param>
    /// <param name="layer">The layer the collider belongs to.</param>
    /// <returns>The collider, ready to be attached to an entity.</returns>
    /// <remarks>
    /// A pair of two colliders is answered when the layer of one of them is in the mask of the other, so two colliders
    /// that both answer their own layer see each other. A game that wants a one-way answer — a shot that a wall stops but
    /// a wall that the shot does not move — sets the mask of the wall to answer nothing or leaves the wall immovable and
    /// lets the resolution push the other body.
    /// </remarks>
    public static ColliderComponent Of(Vector2 size, CollisionLayer layer) => new()
    {
        Size = size,
        Layer = layer,
        Mask = layer,
    };
}

