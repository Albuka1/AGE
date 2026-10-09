using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One line of a laid out text: the characters it holds, its size and where it starts inside the box.
/// </summary>
/// <remarks>
/// The position is relative to the top-left corner of the box that the text was laid out into, so a caller draws the line
/// at its own position plus this one, and it already holds the alignment. The characters are a copy of the part of the text
/// that the line holds, because a line may be shortened with an ellipsis, which is not a part of the text that was laid out.
/// </remarks>
public readonly struct TextLine
{
    /// <summary>Initializes a line of a laid out text.</summary>
    /// <param name="text">The characters of the line, without the line break that ended it.</param>
    /// <param name="size">The size of the line, in pixels.</param>
    /// <param name="position">The position of the top-left corner of the line, relative to the box.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TextLine(string text, Vector2 size, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Size = size;
        Position = position;
    }

    /// <summary>Gets the characters of the line, without the line break that ended it.</summary>
    public string Text { get; }

    /// <summary>Gets the size of the line, in pixels.</summary>
    public Vector2 Size { get; }

    /// <summary>Gets the position of the top-left corner of the line, relative to the box.</summary>
    public Vector2 Position { get; }
}
