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

    /// <summary>Gets the height of the panel as it is drawn this frame, in pixels, which is what a game offsets its own overlay by.</summary>
    /// <remarks>
    /// The height follows the rows the panel draws, so it grows with the output and the suggestions and shrinks to the header
    /// when the console closes. A panel that is not there answers zero, so a game that adds it to a margin draws at the top of
    /// the frame again once the console is gone.
    /// </remarks>
    public float PanelHeight => _panelHeight;

    /// <summary>Reads the keys and the characters of one frame, which is what opens the console, runs a line and takes a suggestion.</summary>
    /// <param name="frame">The time of the frame, which the animation of the panel and the repeat of a held key are measured over.</param>
    /// <remarks>Call it once per frame from the render callback of the loop, before the passes are rendered.</remarks>
    public void Update(in GameTime frame)
    {
        _frameTime = frame.Total;

        bool opened = _input.IsKeyPressed(OpenKey);

        if (opened)
        {
            Toggle();
        }

        if (!_console.IsOpen)
        {
            // A closing panel is already gone as far as the game is concerned, and the clock was put back by the toggle that
            // closed it: the keys of this frame belong to the game again.
            return;
        }

        if (_input.IsKeyPressed(Key.Escape))
        {
            Toggle();
            return;
        }

        // The frame that opened the console drops the characters of that frame, because the key that opened it produces one of
        // its own: a console of a terminal does not print the grave accent that summoned it.
        if (opened)
        {
            return;
        }

        foreach (char character in _text.TypedCharacters)
        {
            _console.Type(character);
            _suggested = -1;
        }

        // A control key that edits the line is read before the plain keys, because Control is held while it is pressed: a person
        // that presses Control and C copies rather than typing a C, and the console of a terminal does the same.
        bool control = _input.IsKeyDown(Key.ControlLeft) || _input.IsKeyDown(Key.ControlRight);

        if (control && Edit())
        {
            _suggested = -1;
            return;
        }

        if (Repeats(Key.Backspace))
        {
            _console.Backspace();
            _suggested = -1;
        }

        if (Repeats(Key.Delete))
        {
            _console.Delete();
            _suggested = -1;
        }

        // The left and right keys walk the caret, and the home and end keys take it to the ends of the line: what a person editing a
        // line of a console reaches for, which a history does not use because the up and down keys already walk that.
        CaretKeys();

        // The down and up keys walk the rows of suggestions while a command is being named, and the history of the console when
        // it is not: an empty line names every command, and walking a list of all of them would hide the history behind it.
        bool naming = _console.Input.Length > 0;

        if (Repeats(Key.Down))
        {
            if (!naming || !MoveSuggestion(1))
            {
                _console.RecallNext();
            }
        }
        else if (Repeats(Key.Up))
        {
            if (!naming || !MoveSuggestion(-1))
            {
                _console.RecallPrevious();
            }
        }

        // The page keys walk the output, which is what reads a log that is longer than the panel. The scroll is measured in rows
        // and the panel clamps it to what there is, so holding a page key settles on an end rather than running away from it.
        if (Repeats(Key.PageUp))
        {
            _scrolled += Math.Max(1, _visibleLines - 1);
        }
        else if (Repeats(Key.PageDown))
        {
            _scrolled -= Math.Max(1, _visibleLines - 1);
        }

        // The wheel walks the output a row at a time, which is what a person reaches for first: a notch away from them scrolls
        // back through the log, and one toward them walks forward to the newest line.
        float wheel = _input.MouseWheel;

        if (wheel != 0f)
        {
            _scrolled += (int)MathF.Round(wheel);
        }

        // Tab takes the chosen suggestion, and completes the word when there is nothing to take: the rows are drawn under the
        // line, which is where a person reads the choice, so the list is answered before the completion.
        if (_input.IsKeyPressed(Key.Tab) && !TakeSuggestion())
        {
            _console.Complete();
            _suggested = -1;
        }

        if (_input.IsKeyPressed(Key.Enter))
        {
            _console.Submit();
            _suggested = -1;

            // A line that was run scrolls the output back to its end, which is where the answer to it is written.
            _scrolled = 0;
        }
    }

    /// <summary>Returns a value indicating whether a key acts on this frame, which is on the frame it went down and then at the repeat rate while it is held.</summary>
    /// <param name="key">The key to read.</param>
    /// <remarks>
    /// A key that is held acts once, then again after a short delay and then every repeat interval, which is what makes a
    /// backspace that is held erase the line rather than one character. The device reports a key that is held as down and not as
    /// pressed, so a console of its own counts the time rather than waiting for a transition that never comes.
    /// </remarks>
    private bool Repeats(Key key)
    {
        if (_input.IsKeyPressed(key))
        {
            _repeating[key] = _frameTime + RepeatDelay;
            return true;
        }

        if (!_input.IsKeyDown(key))
        {
            _repeating.Remove(key);
            return false;
        }

        // A key that is held without a recorded repeat, which happens on the frame the panel opened while the key was already
        // down, starts its repeat here rather than acting on that frame.
        if (!_repeating.TryGetValue(key, out double next))
        {
            _repeating[key] = _frameTime + RepeatDelay;
            return false;
        }

        if (_frameTime < next)
        {
            return false;
        }

        // The next time is taken from the last one rather than from this frame, so a frame that ran long does not push the repeat
        // far into the future: the key keeps its own pace rather than the pace of the display.
        while (next <= _frameTime)
        {
            next += RepeatInterval;
        }

        _repeating[key] = next;
        return true;
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
