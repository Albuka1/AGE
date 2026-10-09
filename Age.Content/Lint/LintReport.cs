namespace Age.Content.Lint;

/// <summary>
/// What a lint found: how much content it read and what is wrong with it.
/// </summary>
/// <param name="Count">The number of prototypes that the content holds, which is zero when a document stopped the reading.</param>
/// <param name="Problems">The mistakes, in the order they were found. The first one is the one that stopped the reading, when it did.</param>
public sealed record LintReport(int Count, IReadOnlyList<LintProblem> Problems)
{
    /// <summary>Gets a value indicating whether the content is sound.</summary>
    public bool IsClean => Problems.Count == 0;
}
