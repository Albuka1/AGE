namespace Age.Content.Locale;

/// <summary>
/// Selects the form of a message for a count, in the language that is being written.
/// </summary>
/// <remarks>
/// <para>
/// The forms are not a property of the number but of the language, which is why this is a table rather than arithmetic: Russian
/// writes «1 предмет», «2 предмета», «5 предметов» and English writes «1 item» and «2 items», and no single rule covers both. The
/// rules here are the ones of the languages the engine ships, taken from CLDR, and a language that is not in the table uses
/// <see cref="PluralCategory.Other"/>, which is what a message that is not written for a count of its own falls back to.
/// </para>
/// <para>
/// A language whose rules are missing is not an error: a game adds its own language by writing its strings and, when its
/// language counts differently, one line in this table.
/// </para>
/// </remarks>
public static class PluralRules
{
    /// <summary>Returns the form of a message that a language writes for a count.</summary>
    /// <param name="language">The language, such as <c>en</c> or <c>ru</c>.</param>
    /// <param name="count">The count the message is written for.</param>
    /// <returns>The form to write, which is <see cref="PluralCategory.Other"/> when the language is not known.</returns>
    /// <exception cref="ArgumentException">The language is null, empty or whitespace.</exception>
    public static PluralCategory Category(string language, long count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return language switch
        {
            // English: one for exactly one, other for everything else, which covers zero.
            "en" => count == 1 ? PluralCategory.One : PluralCategory.Other,

            // Russian: one for 1, 21, 31 and the like, few for 2 to 4, and many for everything else, which covers zero, 5 to
            // 20 and the tens that end in zero. The form 'other' is what a language writes for a count that is not whole, and
            // no such count reaches this rule: a caller passes whole numbers of things.
            "ru" => Russian(count),

            _ => PluralCategory.Other,
        };
    }

    /// <summary>Returns the form of a message that a document may write under a key, or null when the word is not a form.</summary>
    /// <param name="word">The word the document wrote after the dot of a key.</param>
    /// <returns>The category the word names, or null when it names an attribute rather than a form.</returns>
    public static PluralCategory? TryParse(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        return word switch
        {
            "zero" => PluralCategory.Zero,
            "one" => PluralCategory.One,
            "two" => PluralCategory.Two,
            "few" => PluralCategory.Few,
            "many" => PluralCategory.Many,
            "other" => PluralCategory.Other,
            _ => null,
        };
    }

    /// <summary>Returns the name that a document writes for a form, which is what a message about a missing form mentions.</summary>
    /// <param name="category">The form.</param>
    /// <returns>The name of the form, in the lower case a document writes it in.</returns>
    public static string Name(PluralCategory category) => category switch
    {
        PluralCategory.Zero => "zero",
        PluralCategory.One => "one",
        PluralCategory.Two => "two",
        PluralCategory.Few => "few",
        PluralCategory.Many => "many",
        _ => "other",
    };

    /// <summary>Returns the form of a message that Russian writes for a count.</summary>
    private static PluralCategory Russian(long count)
    {
        long units = count % 10;
        long tens = count % 100;

        if (units == 1 && tens != 11)
        {
            return PluralCategory.One;
        }

        if (units is >= 2 and <= 4 && tens is not (12 or 13 or 14))
        {
            return PluralCategory.Few;
        }

        // Everything that is not one or few takes the form of many, which covers zero, 5 to 20 and the tens that end in zero.
        // The form of 'other' is what a language writes for a count that is not whole, and no such count reaches this rule.
        return PluralCategory.Many;
    }
}
