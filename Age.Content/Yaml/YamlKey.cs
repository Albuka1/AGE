namespace Age.Content;

/// <summary>The names of a mapping of a document, which are read the way YAML reads them.</summary>
internal static class YamlKey
{
    /// <summary>Splits a line into the name it holds and the value behind the colon.</summary>
    /// <param name="content">The line of the document, without its indentation.</param>
    /// <param name="name">Receives the name, without the quotes of the document.</param>
    /// <param name="rest">Receives the value behind the colon, which is empty when the value is a block below.</param>
    /// <returns><see langword="true"/> when the line is a name and a value rather than a scalar.</returns>
    /// <remarks>A colon belongs to the name when a space follows it, which is what keeps a word such as <c>level:2</c> a value.</remarks>
    public static bool TrySplit(string content, out string name, out string rest)
    {
        for (var index = 0; index < content.Length; index++)
        {
            char character = content[index];

            if (character == '"' || character == '\'')
            {
                int closing = content.IndexOf(character, index + 1);

                if (closing < 0)
                {
                    name = string.Empty;
                    rest = string.Empty;
                    return false;
                }

                index = closing;
                continue;
            }

            if (character == ':' && (index + 1 == content.Length || content[index + 1] == ' '))
            {
                string key = content[..index].Trim();

                if (key.Length == 0)
                {
                    break;
                }

                name = Unquote(key);
                rest = content[(index + 1)..].Trim();
                return true;
            }
        }

        name = string.Empty;
        rest = string.Empty;
        return false;
    }

    /// <summary>Removes the quotes around a name, which a document may write to hold a character that would end it.</summary>
    /// <param name="text">The text of the name.</param>
    /// <returns>The name without its quotes.</returns>
    public static string Unquote(string text)
    {
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            return text[1..^1];
        }

        return text;
    }
}
