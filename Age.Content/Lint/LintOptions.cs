namespace Age.Content.Lint;

/// <summary>
/// The folders that one lint reads, which is what a build asks about its content.
/// </summary>
/// <remarks>
/// A lint reads what it is asked about rather than everything: a game that has no sheets says so by leaving the folder out, and
/// a build that only wants its strings checked reads one folder. A folder that is null is not read at all, so its report is not
/// in the result and a mistake of it cannot be reported.
/// </remarks>
/// <example>
/// <code>
/// LintResult result = linter.Lint(new LintOptions
/// {
///     Root = "Resources",
///     Prototypes = "Prototypes",
///     Sheets = "Textures",
///     Locales = "Locale",
/// });
/// </code>
/// </example>
public sealed record LintOptions
{
    /// <summary>Gets the game root, which every other folder is resolved inside.</summary>
    public required string Root { get; init; }

    /// <summary>Gets the folder of the documents of the content, or null to read none.</summary>
    public string? Prototypes { get; init; }

    /// <summary>Gets the folder of the sheets of the build and the images beside them, or null to read none.</summary>
    public string? Sheets { get; init; }

    /// <summary>Gets the folder of the languages of the game, or null to read none.</summary>
    public string? Locales { get; init; }
}
