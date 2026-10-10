namespace Age.Content.Lint;

/// <summary>
/// What one pass of a lint found: how much content it read and what is wrong with it.
/// </summary>
/// <param name="Area">The part of the content that was read, which is what a mistake is a mistake of.</param>
/// <param name="Count">The number of documents that the pass read, which is zero when a document stopped the reading.</param>
/// <param name="Problems">The mistakes, in the order they were found. The first one is the one that stopped the reading, when it did.</param>
public sealed record LintReport(LintArea Area, int Count, IReadOnlyList<LintProblem> Problems)
{
    /// <summary>Gets a value indicating whether the content is sound.</summary>
    public bool IsClean => Problems.Count == 0;
}

/// <summary>
/// Everything a lint found, grouped by the pass that found it.
/// </summary>
/// <remarks>
/// <para>
/// A build reads a prototype, a sheet and a string with three different rules, so the mistakes of the three are kept apart
/// rather than collected in one list: a person who is told to fix a sheet is not sent looking for a key of a language, and a
/// program that writes its own report can name the pass that failed. The order of the reports is the order of the passes, which
/// is the order a build makes them in.
/// </para>
/// <para>
/// A lint is a tool rather than a part of a game, and this is what its caller reads: <see cref="IsClean"/> answers whether
/// everything is sound, <see cref="Count"/> how much was read, and <see cref="Problems"/> every mistake with the pass it came
/// from. The reports of the passes that nothing was asked about are not in the collection at all, so a tool that reads two of
/// the three still answers the same questions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// LintResult result = linter.Lint(options);
///
/// if (!result.IsClean)
/// {
///     foreach (LintReport report in result.Reports)
///     {
///         foreach (LintProblem problem in report.Problems)
///         {
///             Console.Error.WriteLine($"[{report.Area}] {problem}");
///         }
///     }
/// }
/// </code>
/// </example>
public sealed class LintResult
{
    private readonly IReadOnlyList<LintReport> _reports;

    /// <summary>Initializes the result of a lint from the reports of its passes, in the order they were made.</summary>
    /// <param name="reports">The reports of the passes that read something.</param>
    /// <exception cref="ArgumentNullException">The reports are null.</exception>
    public LintResult(IReadOnlyList<LintReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        _reports = reports;
    }

    /// <summary>Gets the reports of the passes, in the order they were made.</summary>
    public IReadOnlyList<LintReport> Reports => _reports;

    /// <summary>Gets a value indicating whether every pass found the content sound.</summary>
    public bool IsClean => _reports.All(report => report.IsClean);

    /// <summary>Gets the number of documents that every pass read together.</summary>
    public int Count => _reports.Sum(report => report.Count);

    /// <summary>Gets every mistake of every pass, in the order the passes were made.</summary>
    /// <remarks>A tool that reports a mistake and the pass it belongs to reads <see cref="Reports"/> rather than this, because a problem does not know which pass found it.</remarks>
    public IEnumerable<LintProblem> Problems => _reports.SelectMany(report => report.Problems);

    /// <summary>Gets the number of mistakes that every pass found together.</summary>
    public int ProblemCount => _reports.Sum(report => report.Problems.Count);
}
