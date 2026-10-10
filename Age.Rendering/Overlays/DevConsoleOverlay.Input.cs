using Age.Core;

namespace Age.Rendering;

/// <summary>Reads the keys and the characters of the console, which is the half of it that is not drawing.</summary>
public sealed partial class DevConsoleOverlay
{
    /// <summary>Gets or sets the key that opens and closes the console. The default is the grave accent, the key above Tab.</summary>
    /// <remarks>Escape closes the console as well, so a game that keeps Escape for a menu still has a way out.</remarks>
    public Key OpenKey { get; set; } = Key.GraveAccent;

    /// <summary>Gets or sets how long the panel takes to slide in and out, in seconds. The default is 0.12 seconds.</summary>
    /// <remarks>A value of zero draws the panel at once, which suits a game that finds the motion distracting.</remarks>
    public float AnimationDuration { get; set; } = 0.12f;

    /// <summary>Gets or sets the number of output lines that are drawn above the line that is being typed. The default is 14.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public int VisibleLines
    {
        get => _visibleLines;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _visibleLines = value;
        }
    }

    /// <summary>Gets or sets the number of suggestions that are listed under the line that is being typed. The default is 8.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public int VisibleSuggestions
    {
        get => _visibleSuggestions;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _visibleSuggestions = value;
        }
    }

    /// <summary>Gets a value indicating whether the panel is open or on its way in, which is when a game keeps its input away.</summary>
    public bool IsVisible => _console.IsOpen || _closing;

    /// <summary>Reads the keys and the characters of one frame, which is what opens the console, runs a line and takes a suggestion.</summary>
    /// <param name="frame">The time of the frame, which the animation of the panel is measured over.</param>
    /// <remarks>Call it once per frame from the render callback of the loop, before the passes are rendered.</remarks>
    public void Update(in GameTime frame)
    {
        _frameTime = frame.Total;

        if (_input.IsKeyPressed(OpenKey))
        {
            Toggle();
        }

        if (!_console.IsOpen)
        {
            // A closing panel is already gone as far as the game is concerned: the keys of this frame belong to the game again.
            ApplyPause();
            return;
        }

        if (_input.IsKeyPressed(Key.Escape))
        {
            Toggle();
            return;
        }

        foreach (char character in _text.TypedCharacters)
        {
            _console.Type(character);
            _suggested = -1;
        }

        // Tab takes the chosen suggestion, and completes the word when there is nothing to take: the rows are drawn under the
        // line, which is where a person reads the choice, so the list is answered before the completion.
        if (_input.IsKeyPressed(Key.Tab) && !TakeSuggestion())
        {
            _console.Complete();
            _suggested = -1;
        }

        // The up and down keys walk the rows while a command is being named, and the history of the console otherwise: an empty
        // line names every command, and walking a list of all of them would hide the history behind it.
        bool naming = _console.Input.Length > 0;

        if (_input.IsKeyPressed(Key.Down))
        {
            if (!naming || !MoveSuggestion(1))
            {
                _console.RecallNext();
            }
        }
        else if (_input.IsKeyPressed(Key.Up))
        {
            if (!naming || !MoveSuggestion(-1))
            {
                _console.RecallPrevious();
            }
        }

        if (_input.IsKeyPressed(Key.Enter))
        {
            _console.Submit();
            _suggested = -1;
        }

        if (_input.IsKeyPressed(Key.Backspace))
        {
            _console.Backspace();
            _suggested = -1;
        }
    }

    /// <summary>Opens the console when it is closed and closes it when it is open, which is what the open key does.</summary>
    private void Toggle()
    {
        if (_console.IsOpen)
        {
            _console.Close();
            _closing = true;
            _closedAt = _frameTime;
            _suggested = -1;
        }
        else
        {
            _console.Open();
            _closing = false;
            _openedAt = _frameTime;
        }

        ApplyPause();
    }

    /// <summary>Stands the clock still while the console is open, and puts it back the way it was when it closes.</summary>
    /// <remarks>A game that was already paused stays paused, because the overlay remembers what it found.</remarks>
    private void ApplyPause()
    {
        if (_console.IsOpen)
        {
            _wasPaused = _timestep.Paused;
            _timestep.Paused = true;
            return;
        }

        _timestep.Paused = _wasPaused;
    }
}
