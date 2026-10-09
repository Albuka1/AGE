namespace Age.Content.Locale;

/// <summary>
/// The forms that a message may hold for a count, which are the categories the languages of the engine select between.
/// </summary>
/// <remarks>
/// The names are the ones the documents write, in the lower case the rest of the content uses. English selects between
/// <see cref="One"/> and <see cref="Other"/>, Russian between <see cref="One"/>, <see cref="Few"/>, <see cref="Many"/> and
/// <see cref="Other"/>, and a language that the engine does not know uses <see cref="Other"/> for every count.
/// </remarks>
public enum PluralCategory
{
    /// <summary>No items at all, which some languages write on its own.</summary>
    Zero,

    /// <summary>One item.</summary>
    One,

    /// <summary>Two items, which some languages have a word for.</summary>
    Two,

    /// <summary>A few items, which is what Russian writes for two to four.</summary>
    Few,

    /// <summary>Many items, which is what Russian writes for zero, for five to twenty, and for the tens that end in zero.</summary>
    Many,

    /// <summary>Every other count, which is the form a language falls back to.</summary>
    Other,
}
