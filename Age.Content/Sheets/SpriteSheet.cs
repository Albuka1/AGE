using System.Diagnostics.CodeAnalysis;
using Age.Core;

namespace Age.Content.Sheets;

/// <summary>
/// A sprite sheet as its document declares it: the image, the grid of cells over it, and the states a game plays.
/// </summary>
/// <remarks>
/// <para>
/// A sheet is the one place where the frames of a character live, so a game never writes a normalized coordinate: the grid
/// is declared here and the region of a frame is arithmetic over it. The image and its document travel together, which is
/// what makes a sprite sheet content rather than a picture a programmer slices by hand.
/// </para>
/// <para>
/// A state is a row of the grid and the frames at its start, so a state of four frames uses the first four cells of its
/// row. A direction is a state of its own in this engine: a character that walks in four directions declares four states,
/// which reads better than a count of directions and a mask of frames.
/// </para>
/// </remarks>
/// <param name="Image">The path of the image, relative to the game root.</param>
/// <param name="Cell">The size of one cell of the grid, in pixels.</param>
/// <param name="Columns">The number of columns of the grid.</param>
/// <param name="Rows">The number of rows of the grid.</param>
/// <param name="States">The states of the sheet by name, in the order the document declared them.</param>
/// <param name="File">The sidecar the sheet was read from, which an error mentions.</param>
/// <param name="License">The licence that the art of the sheet comes with, or null when the document does not say.</param>
/// <param name="Copyright">Who the art of the sheet belongs to, or null when the document does not say.</param>
public sealed record SpriteSheet(
    string Image,
    Vector2 Cell,
    int Columns,
    int Rows,
    IReadOnlyDictionary<string, SpriteSheetState> States,
    string File,
    string? License,
    string? Copyright)
{
    /// <summary>The version of the format of a sheet that this build reads, which a document declares in its <c>version</c> field.</summary>
    /// <remarks>A document of a version this build does not know is refused rather than read as if it were this one, so a change to the format is a message instead of a frame that draws the wrong cell.</remarks>
    public const int CurrentVersion = 1;

    /// <summary>Gets the number of states that the sheet declares.</summary>
    public int Count => States.Count;

    /// <summary>Returns a state of the sheet by name.</summary>
    /// <param name="name">The name of the state.</param>
    /// <param name="state">Receives the state when the sheet declares it.</param>
    /// <returns><see langword="true"/> when the sheet declares a state of that name.</returns>
    public bool TryGetState(string name, [NotNullWhen(true)] out SpriteSheetState? state) => States.TryGetValue(name, out state);

    /// <summary>Returns the region of one frame of a state, in the normalized coordinates a renderer maps onto a quad.</summary>
    /// <param name="state">The state that holds the frame.</param>
    /// <param name="frame">The frame inside the state, counting from zero.</param>
    /// <returns>The region, where (0, 0) is the top-left corner of the image and (1, 1) its bottom-right.</returns>
    /// <exception cref="ArgumentNullException">The state is null.</exception>
    /// <remarks>A frame outside the state is not refused here, because the one thing that asks for a frame in the middle of a frame is animation: it clamps before it asks.</remarks>
    public Rect Region(SpriteSheetState state, int frame)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new Rect(
            new Vector2((float)frame / Columns, (float)state.Row / Rows),
            new Vector2(1f / Columns, 1f / Rows));
    }

    /// <inheritdoc />
    public override string ToString() => $"the sheet of '{Image}', {Columns} by {Rows} cells of {Cell.X} by {Cell.Y}";
}

/// <summary>
/// One state of a sheet: the row of the grid it lies on, the frames it holds, and how fast it plays.
/// </summary>
/// <param name="Name">The name the document declared.</param>
/// <param name="Row">The row of the grid, counting from zero at the top.</param>
/// <param name="Frames">The number of frames, which are the first cells of the row.</param>
/// <param name="Delay">The seconds a frame stays on screen, which is the length of every frame of a state that plays evenly.</param>
/// <param name="Delays">The seconds each frame stays on screen, one per frame, or null when the state gives one length for all of them.</param>
/// <param name="Loop">A value indicating whether the state starts over at its last frame.</param>
public sealed record SpriteSheetState(string Name, int Row, int Frames, float Delay, IReadOnlyList<float>? Delays, bool Loop)
{
    /// <summary>Returns the seconds one frame of the state stays on screen.</summary>
    /// <param name="frame">The frame inside the state, counting from zero.</param>
    /// <returns>The seconds the frame stays on screen.</returns>
    /// <remarks>
    /// A state may give every frame a length of its own, which is what an attack needs: a wind-up, a strike and a recovery
    /// are three different lengths, and one number for the whole state makes the strike as slow as the wind-up. A state that
    /// gives one length keeps it once rather than a list of one number per frame, because a state of a thousand frames that
    /// plays evenly is thousands of copies of one number.
    /// </remarks>
    public float DelayOf(int frame) =>
        Delays is null ? Delay : Delays[Math.Clamp(frame, 0, Delays.Count - 1)];

    /// <inheritdoc />
    public override string ToString() => $"the state '{Name}'";
}
