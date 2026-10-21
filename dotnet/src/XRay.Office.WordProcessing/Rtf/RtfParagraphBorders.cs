using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;

namespace XRay.Office.WordProcessing.Rtf;

/// <summary>One side of an RTF paragraph border, as its control words have stated it so far.</summary>
/// <param name="Style">
/// Word's border number from the last style word (<see cref="RtfPageGeometry.StyleOf"/>), 0 while none has
/// been seen — and a side with no style word draws no line, exactly as writerfilter's <c>BorderHandler</c>
/// starts from line type 0 (<c>sw/source/writerfilter/dmapper/BorderHandler.cxx</c>:38-43).
/// </param>
/// <param name="Twips"><c>\brdrw</c>'s own number, twips; 0 when unstated, which is 0.75 pt once drawn.</param>
/// <param name="ColourIndex"><c>\brdrcf</c>, an index into the colour table; null for black.</param>
/// <param name="SpaceTwips"><c>\brsp</c>, twips.</param>
internal readonly record struct RtfBorderDraft(int Style, int Twips, int? ColourIndex, int SpaceTwips);

/// <summary>
/// A paragraph's five border sides as RTF states them: top, left, bottom, right and between.
/// </summary>
/// <remarks>
/// The order and the indices are writerfilter's own, <c>getParagraphBorder</c>
/// (<c>sw/source/writerfilter/rtftok/rtfdocumentimpl.cxx</c>:93-100), so a side number read off a selector
/// means the same thing here as there. A null side is one no selector named.
/// </remarks>
internal readonly record struct RtfBorderDrafts(
    RtfBorderDraft? Top, RtfBorderDraft? Left, RtfBorderDraft? Bottom, RtfBorderDraft? Right,
    RtfBorderDraft? Between)
{
    /// <summary>The selection <c>\box</c> makes: all four outer sides, described together.</summary>
    public const int Box = 5;

    /// <summary>True when no selector has named any side.</summary>
    public bool IsEmpty => Top is null && Left is null && Bottom is null && Right is null && Between is null;

    /// <summary>The side at one of writerfilter's indices.</summary>
    public RtfBorderDraft? this[int side] => side switch
    {
        0 => Top,
        1 => Left,
        2 => Bottom,
        3 => Right,
        _ => Between,
    };

    /// <summary>These sides with one replaced.</summary>
    public RtfBorderDrafts With(int side, RtfBorderDraft? value) => side switch
    {
        0 => this with { Top = value },
        1 => this with { Left = value },
        2 => this with { Bottom = value },
        3 => this with { Right = value },
        _ => this with { Between = value },
    };

    /// <summary>
    /// The sides as the layout model holds them, or null when no selector named any side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A side whose style says no line becomes a <see cref="ParagraphBorder"/> of zero width that still
    /// carries its <c>\brsp</c>, because Writer measures a side's distance even where the side has no line
    /// — <c>SvxBoxItem::CalcLineSpace(..., bEvenIfNoLine=true)</c>, which is how
    /// <c>SwBorderAttrs::CalcTopLine_</c> and its three siblings ask (<c>sw/source/core/layout/frmtool.cxx</c>:2476-2497).
    /// Whether a box with no line at all counts its distances is decided later, by
    /// <see cref="RtfParagraphBorders.Apply"/>: it does not.
    /// </para>
    /// <para>
    /// The width makes writerfilter's round trip: <c>\brdrw</c> twips to eighths of a point with the one-off
    /// that 2 becomes 1 (<c>rtfdispatchvalue.cxx</c>:1019-1027), then back to twips at
    /// <c>BorderHandler.cxx</c>:60, so 3 twips is drawn as 2 and 7 as 5. <c>\brsp</c> is whole points
    /// (<c>rtfdispatchvalue.cxx</c>:1036-1041, integer division), so 30 twips of spacing is 1 pt.
    /// </para>
    /// </remarks>
    /// <param name="colour">Resolves a colour-table index; null for the automatic colour.</param>
    public ParagraphBorderSet? ToSet(Func<int?, Colour?> colour)
    {
        if (IsEmpty) return null;

        return new ParagraphBorderSet
        {
            Top = Side(Top),
            Left = Side(Left),
            Bottom = Side(Bottom),
            Right = Side(Right),
            Between = Side(Between),
        };

        ParagraphBorder? Side(RtfBorderDraft? stated)
        {
            if (stated is not { } side) return null;

            Length space = Length.FromPoints(Math.Max(0, side.SpaceTwips) / 20);
            int eighths = side.Twips == 2 ? 1 : side.Twips > 1 ? side.Twips * 2 / 5 : Math.Max(0, side.Twips);

            if (side.Style is 0 or 255
                || BorderRules.FromWord(side.Style, Length.FromTwips(eighths * 5 / 2)) is not { } rule)
            {
                return new ParagraphBorder(Length.Zero, space, Colour.Black);
            }

            return new ParagraphBorder(
                rule.Width, space, Length.Zero, colour(side.ColourIndex) ?? Colour.Black, rule.Line);
        }
    }
}

/// <summary>
/// Turns the border sets an RTF read stated into the ones Writer draws and measures: the <c>\brdrbtw</c>
/// emulation, the join of alike boxes, and the box's place <em>inside</em> the paragraph's indents.
/// </summary>
/// <remarks>
/// <para>
/// Not <see cref="ParagraphBorderJoin"/>, for three reasons each measured on the 26.8 daily
/// (<c>dotnet/probes/rtf-borders/</c>).
/// </para>
/// <list type="number">
///   <item>
///     <b>An RTF box narrows its text.</b> Writer's own border model puts a paragraph's left and right rules
///     at its indents and the text inside them (<c>SwBorderAttrs::CalcLeft</c>, <c>frmtool.cxx</c>:2408-2420,
///     adds the left line and its distance); only <c>InvertBorderSpacing</c> grows the box outward instead,
///     and the DOCX filter is what sets that (<c>WriterFilter.cxx</c>:319) — RTF never does. Measured:
///     <c>\box\brdrs\brdrw20\brsp60</c> sets its text at 76.10 against an unbordered 72.10, with the left
///     stroke centred on 72.50, and <c>\li720\ri720</c> moves the whole box to 108–504.
///   </item>
///   <item>
///     <b><c>\brdrbtw</c> is not a rule that joins two boxes.</b> dmapper sets <c>ParaIsConnectBorder</c> false
///     on every paragraph stating one, and on the second of two such paragraphs turns the between line into
///     a <em>top</em> border with its own distance and takes the line, not the distance, off the bottom of
///     the first (<c>DomainMapper.cxx</c>:1981-1992, <c>DomainMapper_Impl.cxx</c>:10463-10485). So the gap
///     is the first's bottom <c>\brsp</c>, the between line and the between <c>\brsp</c>: 3, 0.5 and 5 pt give
///     8.5 pt, and 1 and 5 pt of box spacing on either side give 1.5 and 5.5 — the upper paragraph's.
///   </item>
///   <item>
///     <b>Writer joins on the upper paragraph's flag and on lines, not on the whole set.</b>
///     <c>SwBorderAttrs::CalcJoinedWithNext</c> (<c>frmtool.cxx</c>:2595-2617) asks the upper frame's
///     <c>ParaIsConnectBorder</c> and <c>JoinWithCmp</c> compares the four lines and the left and right
///     extents, never a top or bottom distance. So a box over a box that also states <c>\brdrbtw</c> joins,
///     and the reverse does not.
///   </item>
/// </list>
/// <para>
/// And a box with no line on any side takes no room at all, though each distance counts once one side
/// draws: <c>\brdrb\brdrw30\brsp100</c> alone moves nothing, and the same beside a drawn top moves the
/// next paragraph 5 pt.
/// </para>
/// </remarks>
internal static class RtfParagraphBorders
{
    /// <summary>Resolves the borders of one flow's blocks in place.</summary>
    /// <remarks>
    /// A framed paragraph is left as stated: <see cref="ParagraphFrames"/> moves its borders onto the frame,
    /// as <c>lcl_MoveBorderPropertiesToFrame</c> does, and that is done after this.
    /// </remarks>
    /// <param name="blocks">The flow's blocks, in order.</param>
    public static void Apply(List<PageBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        // What the reader stated, before any step below rewrites a neighbour: the between test and the
        // join both read the upper paragraph's *stated* between, as dmapper and Writer read a property.
        ParagraphBorderSet?[] stated = [.. blocks.Select(block => Eligible(block)?.Borders)];
        ParagraphBorderSet?[] resolved = [.. stated];

        for (int i = 1; i < blocks.Count; i++)
        {
            if (stated[i]?.Between is not { } between || stated[i - 1]?.Between is null) continue;
            if (resolved[i - 1] is not { } upper || resolved[i] is not { } lower) continue;

            resolved[i] = lower with { Top = between };
            resolved[i - 1] = upper with
            {
                Bottom = upper.Bottom is { } bottom ? bottom with { Width = Length.Zero } : null,
            };
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            if (resolved[i] is { } set)
            {
                resolved[i] = set.Draws ? set with { Between = null, InsideIndents = true } : null;
            }
        }

        for (int i = 0; i + 1 < blocks.Count; i++)
        {
            if (stated[i] is not { Between: null }) continue;
            if (resolved[i] is not { } upper || resolved[i + 1] is not { } lower) continue;
            if (!Joins(upper, lower, (PageParagraph)blocks[i], (PageParagraph)blocks[i + 1])) continue;

            resolved[i] = upper with { Bottom = null };
            resolved[i + 1] = lower with { Top = null, JoinsAbove = true };
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            if (Eligible(blocks[i]) is not { } paragraph) continue;

            blocks[i] = paragraph with
            {
                Borders = resolved[i],
                Format = resolved[i] is { } set ? Narrowed(paragraph.DeclaredFormat, set) : paragraph.DeclaredFormat,
            };
        }
    }

    private static PageParagraph? Eligible(PageBlock block)
        => block is PageParagraph { Borders: not null, Source: not ParagraphFrameRequest } paragraph
            ? paragraph
            : null;

    /// <summary>
    /// <c>SwBorderAttrs::JoinWithCmp</c>: the four lines alike, and the left and right extents alike.
    /// </summary>
    /// <remarks>
    /// <c>CmpLines</c> compares lines only (<c>frmtool.cxx</c>:2518-2521), so a top or bottom distance may
    /// differ; <c>CmpLeftRight</c> compares <c>CalcLeft</c> and <c>CalcRight</c>, which are the indent, the
    /// line and its distance together — hence the whole side and the declared indents.
    /// </remarks>
    private static bool Joins(
        ParagraphBorderSet upper, ParagraphBorderSet lower, PageParagraph above, PageParagraph below)
        => SameLine(upper.Top, lower.Top)
           && SameLine(upper.Bottom, lower.Bottom)
           && SameLine(upper.Left, lower.Left) && SameLine(upper.Right, lower.Right)
           && Equals(upper.Left?.Space, lower.Left?.Space)
           && Equals(upper.Right?.Space, lower.Right?.Space)
           && above.DeclaredFormat.StartIndent == below.DeclaredFormat.StartIndent
           && above.DeclaredFormat.EndIndent == below.DeclaredFormat.EndIndent
           && above.DeclaredFormat.IsRightToLeft == below.DeclaredFormat.IsRightToLeft;

    private static bool SameLine(ParagraphBorder? a, ParagraphBorder? b)
    {
        bool drawsA = a is { Draws: true };
        bool drawsB = b is { Draws: true };
        if (!drawsA || !drawsB) return drawsA == drawsB;

        return a!.Value.Width == b!.Value.Width && a.Value.Colour == b.Value.Colour && a.Value.Line == b.Value.Line;
    }

    /// <summary>The paragraph's indents with its left and right sides inside them.</summary>
    /// <remarks>
    /// Physical sides, as <c>CalcLeft</c> takes them: a right-to-left paragraph's left rule narrows its
    /// <em>end</em> indent.
    /// </remarks>
    private static ParagraphFormat Narrowed(ParagraphFormat format, ParagraphBorderSet set)
    {
        Length left = set.Left?.Allowance ?? Length.Zero;
        Length right = set.Right?.Allowance ?? Length.Zero;

        return format.IsRightToLeft
            ? format with { StartIndent = format.StartIndent + right, EndIndent = format.EndIndent + left }
            : format with { StartIndent = format.StartIndent + left, EndIndent = format.EndIndent + right };
    }
}
