namespace Age.Content.Lint;

/// <summary>
/// The part of a content that a lint reads, which is what tells a mistake in a document from one in a sheet or a string.
/// </summary>
/// <remarks>
/// A build reads more than one thing, and a person who is handed a list of mistakes wants to know which pass found them: a
/// document of a prototype, the grid of a sheet, or a string whose key another language does not hold. The word is the name a
/// caller passes to the linter and the heading a report is written under, so the two agree without a second table of names.
/// </remarks>
public enum LintArea
{
    /// <summary>The documents of the content, which are the prototypes and their components.</summary>
    Prototypes,

    /// <summary>The documents of the sheets of a build and the images beside them.</summary>
    Sheets,

    /// <summary>The documents of every language, and what they say about each other.</summary>
    Locales,
}
