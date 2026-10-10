using System.Globalization;

namespace Age.Core;

/// <summary>One named setting that a game reads and writes while it runs.</summary>
/// <param name="Name">The name of the setting, which is what the console and a configuration file use.</param>
/// <param name="Type">The type of the value.</param>
/// <param name="Value">The value itself, which is of the type <paramref name="Type"/>.</param>
/// <param name="Description">A single line that says what the setting does.</param>
public readonly record struct CVar(string Name, Type Type, object Value, string Description)
{
    /// <summary>Gets the value as text, with the invariant culture, which is how the console shows it.</summary>
    public string Text => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>
/// The named settings of a game: a value with a type, a description and a default, which a configuration source and the
/// console can both write.
/// </summary>
/// <remarks>
/// <para>
/// A setting exists so that a parameter can be looked at and changed without rebuilding the game. Register one with its
/// default and read it where it is used, which is what makes a constant of the engine a thing a developer can turn while
/// the game runs.
/// </para>
/// <para>
/// Values are read and written as text with the invariant culture, so a file that says <c>0.5</c> means the same thing
/// on every machine. A setting holds a type that <see cref="Convert"/> can turn text into, which covers the numbers, the
/// flags and text itself.
/// </para>
/// <para>
/// Give the service a console and every setting becomes a command of its own: <c>step</c> shows what it holds now and
/// <c>step 0.02</c> writes it, while the <c>cvars</c> command lists all of them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cvars.Register("step", 1f / 60f, "The fixed step of the clock, in seconds.");
///
/// float step = cvars.Get&lt;float&gt;("step");
/// </code>
/// </example>
public sealed class CVarService
{
    private readonly Dictionary<string, CVar> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConsoleService? _console;
    private bool _listed;

    /// <summary>Initializes the service without a console, so its settings are read and written by code only.</summary>
    public CVarService()
    {
    }

    /// <summary>Initializes the service with the console that every setting becomes a command of.</summary>
    /// <param name="console">The console to register a command for each setting with.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
    public CVarService(IConsoleService console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Gets every setting, in the order they were registered.</summary>
    public IEnumerable<CVar> Values => [.. _values.Values];

    /// <summary>Registers a setting with the value it starts at.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="name">The name of the setting. It has to be unique and must not hold whitespace.</param>
    /// <param name="value">The value it starts at.</param>
    /// <param name="description">A single line that says what the setting does.</param>
    /// <exception cref="ArgumentException">The name is null, empty, holds whitespace or is already registered.</exception>
    /// <exception cref="ArgumentNullException">The value or the description is null.</exception>
    public void Register<T>(string name, T value, string description) where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(description);

        if (name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"The name of a setting cannot hold whitespace, but '{name}' does.", nameof(name));
        }

        if (_values.ContainsKey(name))
        {
            throw new ArgumentException($"A setting named '{name}' is already registered.", nameof(name));
        }

        _values[name] = new CVar(name, typeof(T), value, description);
        Describe();
    }

    /// <summary>Returns the value of a setting.</summary>
    /// <typeparam name="T">The type the setting was registered with.</typeparam>
    /// <param name="name">The name of the setting.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">There is no such setting, or it holds another type.</exception>
    public T Get<T>(string name)
    {
        if (TryGet(name, out T value))
        {
            return value;
        }

        throw new InvalidOperationException($"There is no setting named '{name}' that holds a {typeof(T).Name}. Register it before it is read.");
    }

    /// <summary>Returns the value of a setting as text, which is how the console shows it and how a configuration file writes it.</summary>
    /// <param name="name">The name of the setting.</param>
    /// <returns>The value with the invariant culture, or an empty text when nothing was registered under that name.</returns>
    /// <remarks>An unknown name answers an empty text rather than throwing, because a console shows what a person typed without a value in front of it.</remarks>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    public string GetText(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _values.TryGetValue(name, out CVar cvar) ? cvar.Text : string.Empty;
    }

    /// <summary>Returns the value of a setting when there is one of that type.</summary>
    /// <typeparam name="T">The type the setting was registered with.</typeparam>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the setting exists and holds that type.</returns>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    public bool TryGet<T>(string name, out T value)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_values.TryGetValue(name, out CVar cvar) && cvar.Value is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Writes a setting from text, which is what the console and a configuration file do.</summary>
    /// <param name="name">The name of the setting.</param>
    /// <param name="text">The text to read the value from, with the invariant culture.</param>
    /// <returns><see langword="true"/> when the setting exists and the text holds a value of its type.</returns>
    /// <exception cref="ArgumentNullException">The name or the text is null.</exception>
    /// <remarks>A name that no setting was registered under, and a text that does not fit the type of one, are both reported to the console, which is where the person who typed them looks.</remarks>
    public bool SetFromText(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);

        if (!_values.TryGetValue(name, out CVar cvar))
        {
            _console?.WriteError($"there is no setting named '{name}'. Type 'cvars' for the list.");
            return false;
        }

        if (!TryConvert(cvar, text, out object? parsed))
        {
            _console?.WriteError($"'{text}' is not a {cvar.Type.Name} for the setting '{name}'.");
            return false;
        }

        _values[name] = cvar with { Value = parsed! };
        return true;
    }

    /// <summary>Writes every setting that a configuration source mentions, and leaves the others as they are.</summary>
    /// <param name="settings">The names and the texts a configuration source holds, such as the contents of a settings file.</param>
    /// <returns>The number of settings that were written.</returns>
    /// <exception cref="ArgumentNullException">The settings are null.</exception>
    /// <remarks>A name that no setting was registered under is ignored rather than reported, because a configuration source usually holds more than the settings of the engine, and one that holds a text the type cannot read is left as it was.</remarks>
    public int Apply(IEnumerable<KeyValuePair<string, string>> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var written = 0;

        foreach ((string name, string text) in settings)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(text) &&
                _values.TryGetValue(name, out CVar cvar) && TryConvert(cvar, text, out object? parsed))
            {
                _values[name] = cvar with { Value = parsed! };
                written++;
            }
        }

        return written;
    }

    /// <summary>Returns the settings as lines of the form <c>name = value - [Type] description</c>, which the <c>cvars</c> command prints.</summary>
    public IEnumerable<string> List() =>
        Values.Select(cvar => $"{cvar.Name} = {cvar.Text} - [{cvar.Type.Name}] {cvar.Description}").ToArray();

    /// <summary>Reads a text as the type of a setting, with the invariant culture, and reports whether it fits.</summary>
    private static bool TryConvert(CVar cvar, string text, out object? value)
    {
        try
        {
            value = Convert.ChangeType(text, cvar.Type, CultureInfo.InvariantCulture);
            return value is not null;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>Gives every setting a command of its own and adds the listing once, when there is a console to add them to.</summary>
    private void Describe()
    {
        if (_console is null)
        {
            return;
        }

        if (!_listed)
        {
            _listed = true;

            IConsoleService console = _console;
            console.Register("cvars", "Lists the settings, what they hold and what they are for.", _ => console.WriteLines(List()));
        }

        foreach (CVar cvar in Values)
        {
            if (_console.Commands.Any(command => string.Equals(command.Name, cvar.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string name = cvar.Name;
            _console.Register(name, $"[{cvar.Type.Name}] {cvar.Description}", arguments => SetFromConsole(name, arguments));
        }
    }

    /// <summary>Answers the command of one setting: no argument shows what it holds, and the rest of the line writes it.</summary>
    private void SetFromConsole(string name, IReadOnlyList<string> arguments)
    {
        if (!_values.TryGetValue(name, out CVar cvar))
        {
            return;
        }

        if (arguments.Count == 0)
        {
            _console?.Write($"{name} = {cvar.Text}");
            return;
        }

        SetFromText(name, string.Join(' ', arguments));
    }
}
