namespace Age.Physics;

/// <summary>
/// The groups a collider belongs to, which is what one collider reads to know whether it answers another.
/// </summary>
/// <remarks>
/// <para>
/// A layer is a flag rather than a number, because a thing can be more than one of them at once: a shield that is also
/// a weapon, a door that is also a trap. A game that needs a group the engine does not have adds a name to this enum
/// rather than a string to a document, because a mask is a decision of the game and both sides of a pair have to agree on
/// what it means.
/// </para>
/// <para>
/// The default is <see cref="Default"/> rather than <see cref="None"/>, so a collider that names no layer answers
/// <see cref="Everything"/> with the mask below and takes part in the physics of a game that never set one.
/// </para>
/// </remarks>
[Flags]
public enum CollisionLayer
{
    /// <summary>The collider is in no layer, which is what a mask that answers nothing says.</summary>
    None = 0,

    /// <summary>The layer of a collider that does not name one, so a game that never sets a layer still has collisions.</summary>
    Default = 1 << 0,

    /// <summary>The body of the player, which a fight reads as one thing.</summary>
    Player = 1 << 1,

    /// <summary>The body of an enemy of the player, which is what a weapon of the player answers.</summary>
    Enemy = 1 << 2,

    /// <summary>A wall, a door or a piece of the world that stands still and stops a body that is solid.</summary>
    Wall = 1 << 3,

    /// <summary>An item on the ground or in a shop, which a body that walks over it picks up rather than bumps into.</summary>
    Item = 1 << 4,

    /// <summary>A shot in the air, which a wall stops and a body takes.</summary>
    Projectile = 1 << 5,

    /// <summary>A zone that reports a body inside it without blocking the body, such as a door, a trap or a place of rest.</summary>
    Trigger = 1 << 6,

    /// <summary>Every layer, which is what a collider that does not name a mask answers.</summary>
    Everything = ~0,
}
