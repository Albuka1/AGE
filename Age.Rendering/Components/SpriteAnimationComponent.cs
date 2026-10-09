using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Plays one state of the sprite sheet that the <see cref="SpriteComponent"/> of the entity names, and writes the frame that
/// is on screen into that component.
/// </summary>
/// <remarks>
/// <para>
/// What a state holds — the row it lies on, how many frames it has, how long one stays there and whether it starts over —
/// belongs to the document of the sheet, which is data: this component says only which state is played, how fast, and where
/// the play stands. A <see cref="SpriteAnimationSystem"/> advances it on the time of the simulation, so an animation slows
/// down with the clock and stands still while the game is paused.
/// </para>
/// <para>
/// A state that does not loop stops on its last frame, stands paused and raises
/// <see cref="SpriteAnimationFinishedEvent"/> once, so the one that decides what happens next is a game: it sets
/// <see cref="State"/> to the next state and clears <see cref="Paused"/>, or it removes this component.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(hero, new SpriteComponent { SheetPath = "Textures/Entities/goblin.yml", State = "idle", Color = Color.White });
/// world.Set(hero, new SpriteAnimationComponent { State = "walk" });
/// </code>
/// </example>
[Component("SpriteAnimation")]
public struct SpriteAnimationComponent : IComponent
{
    /// <summary>Gets or sets the state of the sheet to play, or null to play the state that the sprite names.</summary>
    /// <remarks>The state that plays is written into the <see cref="SpriteComponent"/> of the same entity, so what a game changes here is what is drawn.</remarks>
    public string? State;

    /// <summary>Gets or sets the seconds that the current frame has been on screen, which the system writes.</summary>
    public float Time;

    /// <summary>Gets or sets the multiplier of the speed of the state, where zero or less means the speed of the sheet.</summary>
    public float Speed;

    /// <summary>Gets or sets a value indicating whether the play stands still.</summary>
    /// <remarks>Many animations of one entity are one component each, so a game that needs two at once gives the second layer an entity of its own.</remarks>
    public bool Paused;
}
