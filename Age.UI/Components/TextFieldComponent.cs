using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element a text field: a line a person types into, which a settings screen and a name prompt are made of.
/// </summary>
/// <remarks>
/// <para>
/// The component holds the line, where the caret stands and what is selected, and it is the one place the editing rules live: typing
/// inserts at the caret, a backspace removes what is marked or the character before it, a delete removes what is marked or the character
/// at it, and the arrows, home and end move the caret while Shift extends the selection. A system reads the keys and the characters of
/// the frame and hands them here, so the component itself is testable without a keyboard.
/// </para>
/// <para>
/// The field that has focus is the one that reads the keyboard, which is what <see cref="Focused"/> says. A game sets it, or
/// <see cref="TextInputSystem"/> sets it from a press inside the rectangle of the element, so a person clicks a field to type in it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(name, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(200f, 28f), Visible = true });
/// world.Set(name, new TextFieldComponent { Text = "hero", Focused = true, MaxLength = 16 });
/// </code>
/// </example>
[Component("TextField")]
public struct TextFieldComponent : IComponent
{
    /// <summary>Gets or sets the line that is being typed.</summary>
    public string Text;

    /// <summary>Gets or sets the index in <see cref="Text"/> where the next character is inserted.</summary>
    /// <remarks>The value is kept inside the line, so a caller does not clamp it: setting a caret past the end puts it at the end.</remarks>
    public int Caret
    {
        readonly get => _caret;
        set
        {
            _caret = Math.Clamp(value, 0, Length);
            _anchor = _caret;
        }
    }

    /// <summary>Gets or sets a value indicating whether the field reads the keyboard this frame.</summary>
    public bool Focused;

    /// <summary>Gets or sets a value indicating whether the field takes input and may be focused by a click.</summary>
    public bool Interactable;

    /// <summary>Gets or sets a value indicating whether the pointer is over the field.</summary>
    public bool IsHovered;

    /// <summary>Gets or sets the greatest number of characters the field holds, or zero for no limit.</summary>
    public int MaxLength;

    private int _caret;
    private int _anchor;

    /// <summary>Gets the characters of the line, which is empty when the field holds none.</summary>
    public readonly string Value => Text ?? string.Empty;

    /// <summary>Gets the number of characters of the line.</summary>
    private readonly int Length => Value.Length;

    /// <summary>Gets the index where the selected text begins, which is the lesser of the caret and the anchor.</summary>
    public readonly int SelectionStart => Math.Min(_anchor, _caret);

    /// <summary>Gets the number of characters that are selected, which is zero when nothing is selected.</summary>
    public readonly int SelectionLength => Math.Abs(_caret - _anchor);

    /// <summary>Gets the selected text, which is empty when nothing is selected.</summary>
    public readonly string Selection => SelectionLength == 0 ? string.Empty : Value.Substring(SelectionStart, SelectionLength);

    /// <summary>Gets or sets the index in <see cref="Text"/> where the selection is anchored, which is where it began.</summary>
    /// <remarks>The selection runs between this and <see cref="Caret"/> in whichever order they are, so a person who selected backwards and one who selected forwards hold the same text.</remarks>
    public int SelectionAnchor
    {
        readonly get => _anchor;
        set => _anchor = Math.Clamp(value, 0, Length);
    }

    /// <summary>Inserts a character at the caret, replacing what is selected, and leaves the caret behind it.</summary>
    /// <param name="character">The character to insert. A control character is dropped.</param>
    /// <returns><see langword="true"/> when the line changed.</returns>
    /// <remarks>A field whose <see cref="MaxLength"/> is reached takes no more characters, which is what a name of a fixed width needs.</remarks>
    public bool Type(char character)
    {
        if (char.IsControl(character))
        {
            return false;
        }

        RemoveSelection();

        if (MaxLength > 0 && Value.Length >= MaxLength)
        {
            return false;
        }

        string value = Value;
        Text = value.Insert(Math.Clamp(_caret, 0, value.Length), character.ToString());
        _caret++;
        _anchor = _caret;
        return true;
    }

    /// <summary>Removes the selected text, or the character before the caret, and leaves the caret where the removal began.</summary>
    /// <returns><see langword="true"/> when the line changed.</returns>
    public bool Backspace()
    {
        if (RemoveSelection())
        {
            return true;
        }

        if (_caret <= 0 || Value.Length == 0)
        {
            return false;
        }

        string value = Value;
        Text = value.Remove(_caret - 1, 1);
        _caret--;
        _anchor = _caret;
        return true;
    }

    /// <summary>Removes the selected text, or the character at the caret, and leaves the caret where the removal began.</summary>
    /// <returns><see langword="true"/> when the line changed.</returns>
    /// <remarks>A selection is the whole of what a delete removes: the character after it stays, which is what a field that marked a word expects.</remarks>
    public bool Delete()
    {
        if (RemoveSelection())
        {
            return true;
        }

        string value = Value;

        if (_caret >= value.Length)
        {
            return false;
        }

        Text = value.Remove(_caret, 1);
        return true;
    }

    /// <summary>Moves the caret by a number of characters, which is what an arrow key does.</summary>
    /// <param name="by">The number of characters to move by, where a negative number moves toward the start of the line.</param>
    /// <param name="extend">Whether the selection is extended rather than collapsed, which is what holding Shift does.</param>
    /// <returns>The index the caret stands at.</returns>
    public int Move(int by, bool extend)
    {
        int moved = Math.Clamp(_caret + by, 0, Value.Length);
        _caret = moved;

        if (!extend)
        {
            _anchor = moved;
        }

        return moved;
    }

    /// <summary>Moves the caret to a place in the line, which is what home and end do.</summary>
    /// <param name="index">The index to move to, which is kept inside the line.</param>
    /// <param name="extend">Whether the selection is extended rather than collapsed, which is what holding Shift does.</param>
    public void Go(int index, bool extend)
    {
        _caret = Math.Clamp(index, 0, Value.Length);

        if (!extend)
        {
            _anchor = _caret;
        }
    }

    /// <summary>Marks the whole line, which is what a select-all key does.</summary>
    public void SelectAll()
    {
        _anchor = 0;
        _caret = Value.Length;
    }

    /// <summary>Writes a text over the selection, at the caret, which is what a paste does.</summary>
    /// <param name="text">The text to write, whose control characters are dropped. A null or empty text only removes the selection.</param>
    /// <returns><see langword="true"/> when the line changed.</returns>
    public bool Replace(string? text)
    {
        RemoveSelection();

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var written = new System.Text.StringBuilder(text.Length);

        foreach (char character in text)
        {
            if (!char.IsControl(character))
            {
                written.Append(character);
            }
        }

        string addition = written.ToString();
        string value = Value;

        if (MaxLength > 0)
        {
            int room = MaxLength - value.Length;

            if (room <= 0)
            {
                return false;
            }

            if (addition.Length > room)
            {
                addition = addition[..room];
            }
        }

        Text = value.Insert(Math.Clamp(_caret, 0, value.Length), addition);
        _caret += addition.Length;
        _anchor = _caret;
        return true;
    }

    /// <summary>Removes the marked text, leaving the caret where the selection began, and reports whether there was one.</summary>
    /// <returns><see langword="true"/> when something was marked and has been removed.</returns>
    public bool RemoveSelection()
    {
        int start = SelectionStart;
        int length = SelectionLength;

        if (length == 0)
        {
            return false;
        }

        Text = Value.Remove(start, length);
        _caret = start;
        _anchor = start;
        return true;
    }
}

/// <summary>
/// Raised when the text of a field changes, which is what a game reacts to rather than reading the line on every frame.
/// </summary>
/// <param name="Entity">The element that carries the field.</param>
/// <param name="Text">The line after the change.</param>
public readonly record struct TextChangedEvent(Entity Entity, string Text);

/// <summary>
/// Raised when a field that has focus is submitted, which is what pressing Enter in a name prompt is.
/// </summary>
/// <param name="Entity">The element that carries the field.</param>
/// <param name="Text">The line the field holds.</param>
public readonly record struct TextSubmittedEvent(Entity Entity, string Text);

