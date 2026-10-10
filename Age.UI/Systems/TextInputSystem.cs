using Age.Core;
using Age.Input;

namespace Age.UI;

/// <summary>
/// Reads the keys of one frame for the text field that has focus, so a person types into the element they clicked.
/// </summary>
/// <remarks>
/// <para>
/// The system is an <see cref="IFrameSystem"/>: text is a state of the frame, not of the simulation, so it runs once per frame and keeps
/// working while the clock is paused, which is what typing a name into a paused menu needs.
/// </para>
/// <para>
/// Only the field that says it has focus reads the keyboard, and a press inside the rectangle of a field that may be focused gives it
/// focus and takes it from the field before, so one field is typed into at a time. The characters of the frame are handed to the field in
/// order, and the keys that are not characters — the arrows, home, end, shift, backspace, delete and the select-all of Control and A — are
/// read from <see cref="IInputService"/>, exactly as a console reads them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// pipeline.AddFrame(provider.GetRequiredService&lt;TextInputSystem&gt;());
/// </code>
/// </example>
public sealed class TextInputSystem : IFrameSystem
{
    private readonly IInputService _input;
    private readonly ITextInputService _text;

    /// <summary>Initializes the system with the input it reads.</summary>
    /// <param name="input">The keys and the pointer of the frame.</param>
    /// <param name="text">The characters that were typed during the frame.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TextInputSystem(IInputService input, ITextInputService text)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(text);

        _input = input;
        _text = text;
    }

    /// <inheritdoc />
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        Entity focused = Focus(world);

        if (!world.IsAlive(focused))
        {
            return;
        }

        ref TextFieldComponent field = ref world.GetRef<TextFieldComponent>(focused);
        bool shift = _input.IsKeyDown(Key.ShiftLeft) || _input.IsKeyDown(Key.ShiftRight);
        bool changed = false;

        // The characters come first, so a frame that typed and then pressed an arrow moves the caret of the line it typed rather than
        // the line of the frame before it.
        foreach (char character in _text.TypedCharacters)
        {
            changed |= field.Type(character);
        }

        changed |= Keys(ref field, shift);

        if (changed)
        {
            world.Events.Raise(new TextChangedEvent(focused, field.Value));
        }
    }

    /// <summary>Reads the keys of a frame for a field, and reports whether any of them changed the line.</summary>
    private bool Keys(ref TextFieldComponent field, bool shift)
    {
        bool changed = false;

        // Control is held for the select-all, and no other key is read while it is.
        if (_input.IsKeyDown(Key.ControlLeft) || _input.IsKeyDown(Key.ControlRight))
        {
            if (_input.IsKeyPressed(Key.A))
            {
                field.SelectAll();
            }

            return false;
        }

        if (_input.IsKeyPressed(Key.Backspace))
        {
            changed |= field.Backspace();
        }

        if (_input.IsKeyPressed(Key.Delete))
        {
            changed |= field.Delete();
        }

        if (_input.IsKeyPressed(Key.Left))
        {
            field.Move(-1, shift);
        }

        if (_input.IsKeyPressed(Key.Right))
        {
            field.Move(1, shift);
        }

        if (_input.IsKeyPressed(Key.Home))
        {
            field.Go(0, shift);
        }

        if (_input.IsKeyPressed(Key.End))
        {
            field.Go(field.Value.Length, shift);
        }

        return changed;
    }

    /// <summary>Returns the field the keyboard is read for, giving focus to the one that was pressed and taking it from the last.</summary>
    /// <returns>The entity of the focused field, or the default entity when no field has focus.</returns>
    /// <remarks>
    /// A press inside the rectangle of a field that may be focused gives it focus; a press anywhere else takes focus from the field that
    /// had it, which is what clicking away from a name prompt does. A field that a game marked focused at the start keeps it until
    /// something else is clicked.
    /// </remarks>
    private Entity Focus(World world)
    {
        Vector2 pointer = _input.MousePosition;
        bool pressed = _input.IsMouseButtonPressed(MouseButton.Left);
        Entity focused = default;

        foreach (Entity entity in world.Enumerate<TextFieldComponent>())
        {
            if (world.Get<TextFieldComponent>(entity).Focused)
            {
                focused = entity;
                break;
            }
        }

        if (!pressed)
        {
            return focused;
        }

        Entity hit = TopMost(world, pointer);

        if (hit == focused)
        {
            return focused;
        }

        if (world.IsAlive(focused))
        {
            ref TextFieldComponent previous = ref world.GetRef<TextFieldComponent>(focused);
            previous.Focused = false;
        }

        if (world.IsAlive(hit))
        {
            ref TextFieldComponent next = ref world.GetRef<TextFieldComponent>(hit);
            next.Focused = true;

            // Clicking into a field puts the caret at the end of what it holds, which is where a person expects to type next: a field
            // that was authored with a text and no caret would otherwise take the first character at its very beginning.
            next.Go(next.Value.Length, extend: false);
            return hit;
        }

        return default;
    }

    /// <summary>Returns the field that the pointer is over and that may be focused, or the default entity when it is over none.</summary>
    private static Entity TopMost(World world, Vector2 pointer)
    {
        Entity topMost = default;
        int topZ = int.MinValue;

        foreach (Entity entity in world.Enumerate<TextFieldComponent>())
        {
            TextFieldComponent field = world.Get<TextFieldComponent>(entity);

            if (!field.Interactable || !world.Has<RectTransformComponent>(entity))
            {
                continue;
            }

            RectTransformComponent rect = world.Get<RectTransformComponent>(entity);

            if (pointer.X < rect.Position.X || pointer.X > rect.Position.X + rect.Size.X || pointer.Y < rect.Position.Y || pointer.Y > rect.Position.Y + rect.Size.Y)
            {
                continue;
            }

            if (topMost == default || rect.ZOrder >= topZ)
            {
                topMost = entity;
                topZ = rect.ZOrder;
            }
        }

        return topMost;
    }
}

