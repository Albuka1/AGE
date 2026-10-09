using Age.Content.Sheets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Plays the state of a sprite sheet that a <see cref="SpriteAnimationComponent"/> names, on the time of the simulation.
/// </summary>
/// <remarks>
/// <para>
/// What a state holds belongs to the document of the sheet, so this system asks the sheet for the row, the number of frames
/// and the length of one frame rather than reading a copy of them out of the component: a change in the document changes
/// what plays without a change in a game. The frame that is on screen is written into the <see cref="SpriteComponent"/> of
/// the same entity, which is the one place a renderer reads it.
/// </para>
/// <para>
/// The animated entities are read into a buffer before anything is written, because the world must not change while its
/// components are being enumerated. A state of one frame is left alone, a state that loops starts over, and a state that
/// does not loop stops on its last frame, stands paused and raises <see cref="SpriteAnimationFinishedEvent"/> once. Events
/// are queued like every other event and reach their subscribers at the boundary of the step.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The system runs in the fixed step of the world, next to the timers and the tweens.
/// world.Events.Subscribe&lt;SpriteAnimationFinishedEvent&gt;((entity, @event) =&gt;
/// {
///     ref SpriteAnimationComponent animation = ref world.GetRef&lt;SpriteAnimationComponent&gt;(entity);
///     animation.State = "idle";
///     animation.Paused = false;
/// });
/// </code>
/// </example>
public sealed class SpriteAnimationSystem : ISystem
{
    private readonly ISpriteSheetService _sheets;
    private readonly List<Entity> _animated = new();

    /// <summary>Initializes the system with the sheets its animations come from.</summary>
    /// <param name="sheets">The service that reads the documents of the sheets.</param>
    /// <exception cref="ArgumentNullException">The service is null.</exception>
    public SpriteAnimationSystem(ISpriteSheetService sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        _sheets = sheets;
    }

    /// <inheritdoc />
    /// <remarks>The time of the step is the fixed step of the clock, so an animation plays on the simulation rather than on the frames.</remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        _animated.Clear();

        foreach (Entity entity in world.Enumerate<SpriteAnimationComponent>())
        {
            if (!world.Get<SpriteAnimationComponent>(entity).Paused && world.Has<SpriteComponent>(entity))
            {
                _animated.Add(entity);
            }
        }

        foreach (Entity entity in _animated)
        {
            SpriteAnimationComponent animation = world.Get<SpriteAnimationComponent>(entity);
            SpriteComponent sprite = world.Get<SpriteComponent>(entity);
            string state = animation.State ?? sprite.State ?? string.Empty;

            if (sprite.SheetPath is not string sheetPath
                || !_sheets.TryState(sheetPath, state, out SpriteSheetState? declared)
                || declared.Frames <= 1)
            {
                // Nothing to play: a sheet or a state that cannot be read was reported by the service already, and a state
                // of one frame is the frame it holds.
                continue;
            }

            float speed = animation.Speed > 0f ? animation.Speed : 1f;
            animation.Time += (float)time.Delta * speed;

            int frame = sprite.Frame;
            var finished = false;

            while (animation.Time >= declared.Delay)
            {
                animation.Time -= declared.Delay;
                frame++;

                if (frame < declared.Frames)
                {
                    continue;
                }

                if (declared.Loop)
                {
                    frame = 0;
                    continue;
                }

                // The last frame of a state that does not loop stays on screen, and the play stands still until a game
                // says what comes next.
                frame = declared.Frames - 1;
                animation.Paused = true;
                finished = true;
                break;
            }

            world.Set(entity, animation);

            if (frame != sprite.Frame)
            {
                world.GetRef<SpriteComponent>(entity).Frame = frame;
            }

            if (finished)
            {
                world.Events.Raise(entity, new SpriteAnimationFinishedEvent(entity, state));
            }
        }
    }
}
