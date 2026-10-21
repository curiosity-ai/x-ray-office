using XRay.Office.Core.Units;

namespace XRay.Office.Text.Layout;

/// <summary>
/// A paragraph's automatic hyphenation, as Writer's <c>SvxHyphenZoneItem</c> holds it.
/// </summary>
/// <remarks>
/// <para>
/// Present on a <see cref="ParagraphFormat"/> only when hyphenation is on for the paragraph, so a paragraph
/// without it lays out exactly as it always did. Each reader turns its own spelling into this one: RTF's
/// <c>\hyphauto</c> and <c>\hyphpar</c>, DOCX's <c>w:autoHyphenation</c> and <c>w:suppressAutoHyphens</c>,
/// ODF's <c>fo:hyphenate</c> and its siblings.
/// </para>
/// <para>
/// The minima are the paragraph's, applied on top of the dictionary's: Writer hands the hyphenator
/// <c>max(GetMinLead(), 2)</c> and <c>GetMinTrail()</c> (<c>sw/source/core/text/inftxt.cxx</c>:1749-1750),
/// and the pattern file's own <c>LEFTHYPHENMIN</c>/<c>RIGHTHYPHENMIN</c> still bind — which
/// <see cref="Core.Globalization.HyphenationPatterns"/> already applies.
/// </para>
/// </remarks>
/// <param name="MinLeading">Characters that must stay on the line before the hyphen.</param>
/// <param name="MinTrailing">Characters that must move to the next line.</param>
/// <param name="MinWordLength">The shortest word that is hyphenated at all; nought for any length.</param>
/// <param name="Zone">
/// How close to the line's end a blank must be for the line to end there rather than be hyphenated; nought
/// for no zone. See <see cref="LineFiller"/>.
/// </param>
/// <param name="NoCaps">True when a word written all in capitals is not hyphenated.</param>
/// <param name="MaxConsecutive">How many lines in a row may end hyphenated; nought for no limit.</param>
/// <param name="KeepsLastLine">
/// True when the last line of a page or column is not hyphenated: Writer's <c>hyphenation-keep</c> with
/// <c>hyphenation-keep-line</c>, which disables hyphenation in that one line rather than moving it
/// (<c>sw/source/core/text/widorp.cxx</c>:495-600). Writerfilter turns it on for every DOCX and RTF that
/// does not allow hyphenation at the bottom of a page (<c>SettingsTable.cxx</c>:591-606, 780-785). The
/// paginator applies it, through <paramref name="NoHyphenationAt"/>.
/// </param>
/// <param name="NoHyphenationAt">
/// The offsets of lines that must not end hyphenated, or null. Writer's <c>SetNoHyphOffset</c>: set by the
/// paginator for a line that would end a page hyphenated under <paramref name="KeepsLastLine"/>.
/// </param>
public sealed record HyphenationRules(
    int MinLeading = 2,
    int MinTrailing = 2,
    int MinWordLength = 5,
    Length Zone = default,
    bool NoCaps = false,
    int MaxConsecutive = 0,
    bool KeepsLastLine = false,
    IReadOnlySet<int>? NoHyphenationAt = null)
{
    /// <summary>
    /// Writer's defaults for a paragraph that turns hyphenation on and states nothing else.
    /// </summary>
    /// <remarks>
    /// Two, two and five: not the item's own noughts but what <c>SwDocShell::InitNew</c> puts on a new
    /// document's pool default from the linguistic options (<c>sw/source/uibase/app/docshini.cxx</c>:670-676),
    /// which every import starts from. 26.8.1.1's flat export of an imported RTF states exactly these on its
    /// default paragraph style (<c>fo:hyphenation-remain-char-count="2"</c>, <c>push-char-count="2"</c>,
    /// <c>loext:hyphenation-word-char-count="5"</c>).
    /// </remarks>
    public static HyphenationRules Default { get; } = new();
}
