namespace Age.Rendering;

/// <summary>
/// The metrics of a baked font that place a line of its glyphs: the distance to the baseline and the distance between two
/// baselines.
/// </summary>
/// <remarks>
/// <para>
/// A caller that lays out text needs these two numbers together with the advance of the characters it measures, which
/// <see cref="IFontService.Measure"/> answers: a pen starts at the baseline of a line, every glyph moves it by its
/// advance, and the next line starts one line height below the last one.
/// </para>
/// <para>
/// A box that holds one line of text is <see cref="LineHeight"/> pixels tall and its top is <see cref="Ascent"/> pixels
/// above the baseline, which is what <see cref="IFontService.Draw"/> takes as the position of a line. The descent is what
/// remains of a line below the baseline.
/// </para>
/// </remarks>
public readonly struct FontMetrics
{
    /// <summary>Initializes the metrics of a baked font.</summary>
    /// <param name="ascent">The distance from the top of a line to its baseline, in pixels.</param>
    /// <param name="lineHeight">The distance between two baselines, in pixels.</param>
    public FontMetrics(float ascent, float lineHeight)
    {
        Ascent = ascent;
        LineHeight = lineHeight;
    }

    /// <summary>Gets the distance from the top of a line to its baseline, in pixels.</summary>
    public float Ascent { get; }

    /// <summary>Gets the distance between two baselines, in pixels.</summary>
    public float LineHeight { get; }

    /// <summary>Gets the distance from the baseline to the bottom of a line, in pixels.</summary>
    public float Descent => LineHeight - Ascent;
}
