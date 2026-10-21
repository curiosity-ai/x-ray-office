using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;

namespace XRay.Office.Text.Layout;

/// <summary>
/// The character half of a page's text grid: Writer's <c>SwTextGrid::LinesAndChars</c>, which DOCX states
/// as <c>w:docGrid w:type="linesAndChars"</c> or <c>"snapToChars"</c> with a non-zero <c>w:charSpace</c>,
/// and DOC as the section's <c>sprmSClm</c> of 1 or 3.
/// </summary>
/// <remarks>
/// <para>
/// It reaches East Asian text only. Every one of Writer's grid paths — <c>SwFntObj::DrawText</c>,
/// <c>GetTextSize</c> and <c>GetTextBreak</c> (<c>sw/source/core/txtnode/fntcache.cxx</c>:1097-1141,
/// 1745-1789, 2172-2198) — is guarded by <c>SwFontScript::CJK == GetActual()</c>, so a Latin portion on the
/// same line is measured and drawn exactly as it is off the grid. A portion's script is
/// <c>SwScriptInfo</c>'s, where a weak character (a blank, a digit's neighbour punctuation) takes the
/// script of the text before it; see <see cref="AsianCharacters"/>.
/// </para>
/// <para>
/// Both of the formats that state it set <c>MS_WORD_COMP_GRID_METRICS</c> (<c>DomainMapper.cxx</c>:159,
/// <c>ww8par.cxx</c>:1968), and that one setting decides three things: no kern portions align a portion to
/// an absolute page grid (<c>itrform2.cxx</c>:450-461, tdf#149089, so each cell is the character's own),
/// the body is not narrowed to a whole number of cells (<c>pagechg.cxx</c>:127-143), and the
/// non-snapping mode takes Word's arithmetic rather than Writer's (<c>lcl_MsoCompatSnapToGridEdge</c>,
/// <c>sw/source/core/txtnode/justify.cxx</c>:218-253). Both readers also put the document in standard
/// (non-squared) page mode — <c>GRID_STANDARD_MODE</c> (<c>PropertyMap.cxx</c>:2021-2026) and
/// <c>SetDefaultPageMode(false)</c> (<c>ww8par6.cxx</c>:264-267) — so <c>GetGridWidth</c>
/// (<c>pagechg.cxx</c>:2907-2910) is the stated base width and never the line pitch, and the squared-mode
/// branches (<c>itrtxt.cxx</c>:246, the ruby ones in <c>pormulti.cxx</c>) are never taken.
/// </para>
/// <para>
/// The two modes, per character cluster of width <c>w</c> whole twips (a <c>KernArray</c> difference
/// assigned to a <c>tools::Long</c>, so truncated):
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>linesAndChars</b> (<see cref="SnapsToCharacters"/> false): <c>w + (Width − BaseFontHeight)</c>, the
/// glyph at the start of its cell. Word spaces the characters by the difference between the grid and the
/// document's own font size, whatever size the run is set at.
/// </description></item>
/// <item><description>
/// <b>snapToChars</b> (true): <c>Width × ceil(w / Width)</c>, at least one cell (<c>lcl_MinGridWidth</c>,
/// <c>justify.cxx</c>:42-46); the glyph is drawn centred in it, against the following edge for an opening
/// bracket and against the preceding one for a closing bracket, an ideographic comma or full stop
/// (<c>lcl_OffsetFromGridEdge</c>, <c>justify.cxx</c>:48-71). A paragraph on such a grid is not justified at
/// all: <c>SwTextPortion::GetSpaceCnt</c> and <c>CalcSpacing</c> answer nought for every portion
/// (<c>sw/source/core/text/portxt.cxx</c>:1191-1196, 1231-1236).
/// </description></item>
/// </list>
/// </remarks>
/// <param name="Width">The grid's character pitch, whole twips.</param>
/// <param name="BaseFontHeight">
/// The East Asian font size of the default paragraph style, whole twips: what <c>lcl_SnapToGridEdge</c> reads
/// off <c>STR_POOLCOLL_STANDARD</c> (<c>fntcache.cxx</c>:886-894). Only the non-snapping mode reads it.
/// </param>
/// <param name="SnapsToCharacters">True for <c>snapToChars</c>; false for <c>linesAndChars</c>.</param>
public readonly record struct CharacterGrid(Length Width, Length BaseFontHeight, bool SnapsToCharacters)
{
    /// <summary>
    /// Which characters of a paragraph Writer sets in an East Asian portion.
    /// </summary>
    /// <remarks>
    /// The script itemisation <c>GreedyScriptChangeScanner</c> performs
    /// (<c>i18nutil/source/utility/scriptchangescanner.cxx</c>:200-300, 336-360): a character keeps its own
    /// class, a weak one takes the class of the text before it, and weak text that opens the paragraph takes
    /// the first strong class anywhere in it. A <c>w:hint</c> is not consulted, which only matters for a weak
    /// character a document marks East Asian.
    /// </remarks>
    /// <param name="text">The paragraph's text.</param>
    public static bool[] AsianCharacters(ReadOnlySpan<char> text)
    {
        bool[] asian = new bool[text.Length];
        WriterScriptClass current = WriterScriptClass.Weak;

        for (int at = 0; at < text.Length; at++)
        {
            WriterScriptClass own = WriterScripts.ClassOf(CodePointAt(text, at));
            if (own != WriterScriptClass.Weak) { current = own; break; }
        }

        for (int at = 0; at < text.Length; at++)
        {
            WriterScriptClass own = WriterScripts.ClassOf(CodePointAt(text, at));
            if (own != WriterScriptClass.Weak) current = own;

            asian[at] = current == WriterScriptClass.Asian;
        }

        return asian;
    }

    /// <summary>
    /// The cell a cluster of a given advance takes on the grid.
    /// </summary>
    /// <param name="advance">The cluster's own advance; nought or less is returned unchanged.</param>
    public Length Cell(Length advance)
    {
        if (advance <= Length.Zero) return advance;

        long width = advance.Emu / Length.EmuPerTwip;

        return Length.FromTwips(SnapsToCharacters
            ? MinimumCells(width)
            : width + Width.Twips - BaseFontHeight.Twips);
    }

    /// <summary>
    /// How far into its cell a cluster's glyph is drawn: nought except under <see cref="SnapsToCharacters"/>.
    /// </summary>
    /// <param name="advance">The cluster's own advance.</param>
    /// <param name="character">The cluster's first character, which decides the punctuation class.</param>
    public Length Offset(Length advance, char character)
    {
        if (!SnapsToCharacters || advance <= Length.Zero) return Length.Zero;

        long width = advance.Emu / Length.EmuPerTwip;
        long cell = MinimumCells(width);

        return Length.FromTwips(Punctuation(character) switch
        {
            PunctuationClass.None => (cell - width) / 2,
            PunctuationClass.OpenBracket => cell - width,
            _ => 0,
        });
    }

    /// <summary><c>lcl_MinGridWidth</c>: the whole cells a width needs, one at least.</summary>
    private long MinimumCells(long width)
    {
        long pitch = Math.Max(1, Width.Twips);
        long count = width > pitch ? (width - 1) / pitch + 1 : 1;
        return count * pitch;
    }

    /// <summary><c>lcl_WhichPunctuationClass</c> (<c>justify.cxx</c>:25-40).</summary>
    private static PunctuationClass Punctuation(char c)
    {
        if ((c < '、' || c > '。') && (c < '〈' || c > '】')
            && (c < '〔' || c > '〟') && c != '｢' && c != '｣')
        {
            return PunctuationClass.None;
        }

        if (c is '、' or '。') return PunctuationClass.CommaOrFullStop;

        return c is '〉' or '》' or '」' or '』' or '】' or '〕' or '〗'
            or '〙' or '〛' or '〞' or '〟' or '｣'
            ? PunctuationClass.CloseBracket
            : PunctuationClass.OpenBracket;
    }

    private static int CodePointAt(ReadOnlySpan<char> text, int at)
    {
        char c = text[at];
        if (char.IsHighSurrogate(c) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1]))
            return char.ConvertToUtf32(c, text[at + 1]);

        // A low surrogate belongs to the character it completes, and has that character's class.
        if (char.IsLowSurrogate(c) && at > 0 && char.IsHighSurrogate(text[at - 1]))
            return char.ConvertToUtf32(text[at - 1], c);

        return c;
    }

    private enum PunctuationClass
    {
        None,
        OpenBracket,
        CloseBracket,
        CommaOrFullStop,
    }
}
