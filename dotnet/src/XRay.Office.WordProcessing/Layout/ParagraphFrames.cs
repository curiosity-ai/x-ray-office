using XRay.Office.Core.Geometry;
using XRay.Office.Core.Units;

namespace XRay.Office.WordProcessing.Layout;

/// <summary>How a paragraph frame's height is read: Writer's <c>SizeType</c>.</summary>
public enum ParagraphFrameHeightRule
{
    /// <summary>As tall as the frame's text: <c>SizeType::VARIABLE</c>, OOXML's <c>auto</c>.</summary>
    Auto,

    /// <summary>The stated height is a floor: <c>SizeType::MIN</c>, OOXML's <c>atLeast</c>.</summary>
    AtLeast,

    /// <summary>The stated height and no other: <c>SizeType::FIX</c>, OOXML's <c>exact</c>.</summary>
    Exact,
}

/// <summary>Which edge a paragraph frame is aligned to across the page, when it is aligned at all.</summary>
/// <remarks>Writer's <c>HoriOrientation</c>, of which a paragraph frame can state these six.</remarks>
public enum ParagraphFrameHorizontalAlignment
{
    /// <summary><c>HoriOrientation::NONE</c>: placed at a stated distance.</summary>
    None,

    /// <summary><c>LEFT</c>.</summary>
    Left,

    /// <summary><c>CENTER</c>.</summary>
    Centre,

    /// <summary><c>RIGHT</c>.</summary>
    Right,

    /// <summary><c>INSIDE</c>, which a fly without position toggling places as <see cref="Left"/>.</summary>
    Inside,

    /// <summary><c>OUTSIDE</c>, likewise placed as <see cref="Left"/>.</summary>
    Outside,
}

/// <summary>Which edge a paragraph frame is aligned to down the page, when it is aligned at all.</summary>
public enum ParagraphFrameVerticalAlignment
{
    /// <summary><c>VertOrientation::NONE</c>: placed at a stated distance.</summary>
    None,

    /// <summary><c>TOP</c>.</summary>
    Top,

    /// <summary><c>CENTER</c>.</summary>
    Centre,

    /// <summary><c>BOTTOM</c>.</summary>
    Bottom,
}

/// <summary>What a paragraph frame's position is measured from: Writer's <c>RelOrientation</c>.</summary>
public enum ParagraphFrameRelation
{
    /// <summary><c>FRAME</c>: the anchor's column across, its paragraph down. OOXML's <c>text</c>.</summary>
    Text,

    /// <summary><c>PAGE_PRINT_AREA</c>: inside the page margins. OOXML's <c>margin</c>.</summary>
    Margin,

    /// <summary><c>PAGE_FRAME</c>: the sheet. OOXML's <c>page</c>.</summary>
    Page,
}

/// <summary>
/// One layer of a Word paragraph frame's properties, as writerfilter's <c>ParagraphProperties</c> holds
/// them: every field null until the layer states it.
/// </summary>
/// <remarks>
/// <para>
/// A Word <em>paragraph frame</em> is not a shape: it is ordinary body paragraphs stating where they
/// should be put — OOXML's <c>w:framePr</c>, RTF's <c>\absw</c>, <c>\posx</c> and their kin.
/// <c>DomainMapper_Impl::MakeFrameProperties</c>
/// (<c>sw/source/writerfilter/dmapper/DomainMapper_Impl.cxx</c>:1873-2083 on the 26.8 branch) resolves
/// each property separately, taking the first layer that states it — the paragraph's own properties,
/// then its style and that style's parents. The document defaults are not a layer: the chain is
/// <c>lcl_getParent</c>'s, which is styles only.
/// </para>
/// <para>
/// The fields hold what <c>DomainMapper::lcl_attribute</c> stores for each attribute
/// (<c>DomainMapper.cxx</c>:1022-1186), including its side effects: stating an <c>x</c> also states the
/// alignment as <see cref="ParagraphFrameHorizontalAlignment.None"/>, and a <c>y</c> the vertical one,
/// so whichever of a pair comes later in the attribute list wins.
/// </para>
/// </remarks>
public sealed class ParagraphFrameLayer
{
    /// <summary>Twips; zero or less asks for an automatic width.</summary>
    public long? Width { get; set; }

    /// <summary>Twips.</summary>
    public long? Height { get; set; }

    /// <summary>How the height is read.</summary>
    public ParagraphFrameHeightRule? HeightRule { get; set; }

    /// <summary>Twips, from the relation's start edge.</summary>
    public long? X { get; set; }

    /// <summary>The horizontal alignment.</summary>
    public ParagraphFrameHorizontalAlignment? XAlign { get; set; }

    /// <summary>What <see cref="X"/> is measured from.</summary>
    public ParagraphFrameRelation? HorizontalAnchor { get; set; }

    /// <summary>Twips, from the relation's top.</summary>
    public long? Y { get; set; }

    /// <summary>The vertical alignment.</summary>
    public ParagraphFrameVerticalAlignment? YAlign { get; set; }

    /// <summary>What <see cref="Y"/> is measured from.</summary>
    public ParagraphFrameRelation? VerticalAnchor { get; set; }

    /// <summary>What text beside the frame does.</summary>
    public TextWrap? Wrap { get; set; }

    /// <summary>Twips text keeps clear of the frame left and right.</summary>
    public long? HorizontalSpace { get; set; }

    /// <summary>Twips text keeps clear above and below.</summary>
    public long? VerticalSpace { get; set; }

    /// <summary>
    /// <c>w:dropCap</c>: 0 for none, 1 for <c>drop</c>, 2 for <c>margin</c>. Read only on the paragraph
    /// itself, since <c>finishParagraph</c> asks the paragraph's own properties.
    /// </summary>
    public int DropCap { get; set; }

    /// <summary><c>w:lines</c>, the lines a drop cap spans; zero when unstated.</summary>
    public int Lines { get; set; }

    /// <summary>
    /// The paragraph frame the chain resolves to — <c>MakeFrameProperties</c>, property by property.
    /// </summary>
    /// <param name="chain">The layers, innermost first: the paragraph's own, then its style chain.</param>
    public static ParagraphFramePosition Resolve(IReadOnlyList<ParagraphFrameLayer> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        T? First<T>(Func<ParagraphFrameLayer, T?> of) where T : struct
        {
            foreach (ParagraphFrameLayer layer in chain)
            {
                if (of(layer) is { } value) return value;
            }

            return null;
        }

        long width = First(layer => layer.Width is { } w && w >= 0 ? w : (long?)null) ?? 0;
        long? height = First(layer => layer.Height is { } h && h >= 0 ? h : (long?)null);

        // [MS-OE376]: an unstated rule is "at least" when a non-zero height is stated (:1936-1946).
        ParagraphFrameHeightRule rule = First(layer => layer.HeightRule)
            ?? (height is > 0 ? ParagraphFrameHeightRule.AtLeast : ParagraphFrameHeightRule.Auto);

        long? x = First(layer => layer.X);
        ParagraphFrameHorizontalAlignment xAlign =
            First(layer => layer.XAlign) ?? ParagraphFrameHorizontalAlignment.None;

        // ECMA 17.3.1.11's default for an unstated hAnchor is the column (:1972).
        ParagraphFrameRelation hAnchor = First(layer => layer.HorizontalAnchor) ?? ParagraphFrameRelation.Text;

        long? y = First(layer => layer.Y);

        // "Testing indicates that yAlign should be ignored if there is any specified w:y" (:1998).
        ParagraphFrameVerticalAlignment yAlign = y is null
            ? First(layer => layer.YAlign) ?? ParagraphFrameVerticalAlignment.None
            : ParagraphFrameVerticalAlignment.None;

        // An unstated vAnchor is the margin, except that a frame at zero offset and not aligned sits
        // against its paragraph (tdf#157572, tdf#112287; :2010-2030). A stated `text` drops the
        // alignment.
        ParagraphFrameRelation vAnchor =
            (y ?? 0) == 0 && (y is not null || yAlign == ParagraphFrameVerticalAlignment.None)
                ? ParagraphFrameRelation.Text
                : ParagraphFrameRelation.Margin;

        if (First(layer => layer.VerticalAnchor) is { } statedV)
        {
            vAnchor = statedV;
            if (statedV == ParagraphFrameRelation.Text) yAlign = ParagraphFrameVerticalAlignment.None;
        }

        // `WrapTextMode_NONE` — no text beside the frame — when nothing in the chain states one.
        TextWrap wrap = First(layer => layer.Wrap) ?? TextWrap.TopAndBottom;

        long horizontalSpace = First(layer => layer.HorizontalSpace is { } h && h >= 0 ? h : (long?)null) ?? 0;
        long verticalSpace = First(layer => layer.VerticalSpace is { } v && v >= 0 ? v : (long?)null) ?? 0;

        // The side a frame is aligned flush to keeps no distance (:2050-2080), because Writer would
        // otherwise push an aligned frame in by its margin.
        Margins spacing = new(
            Length.FromTwips(xAlign == ParagraphFrameHorizontalAlignment.Left ? 0 : horizontalSpace),
            Length.FromTwips(yAlign == ParagraphFrameVerticalAlignment.Top ? 0 : verticalSpace),
            Length.FromTwips(xAlign == ParagraphFrameHorizontalAlignment.Right ? 0 : horizontalSpace),
            Length.FromTwips(yAlign == ParagraphFrameVerticalAlignment.Bottom ? 0 : verticalSpace));

        return new ParagraphFramePosition(
            Length.FromTwips(Math.Max(0, width)),
            Length.FromTwips(Math.Max(0, height ?? 0)),
            rule,
            hAnchor,
            xAlign,
            Length.FromTwips(x ?? 0),
            vAnchor,
            yAlign,
            Length.FromTwips(y ?? 0),
            wrap,
            spacing);
    }
}

/// <summary>
/// A paragraph frame's resolved properties, which is also what decides which paragraphs share it.
/// </summary>
/// <remarks>
/// <c>finishParagraph</c> compares two consecutive framed paragraphs' <c>MakeFrameProperties</c> output and
/// keeps them in one frame when it is equal (<c>DomainMapper_Impl.cxx</c>:2553, case 7), so this is
/// a value type and that comparison is its equality. Borders are deliberately not in it: two paragraphs
/// with the same position and different borders are one frame.
/// </remarks>
/// <param name="Width">The stated width, or zero for an automatic one.</param>
/// <param name="Height">The stated height, or zero.</param>
/// <param name="HeightRule">How <paramref name="Height"/> is read.</param>
/// <param name="HorizontalAnchor">What the horizontal position is measured from.</param>
/// <param name="HorizontalAlignment">How the frame is aligned across.</param>
/// <param name="X">The offset, when it is not aligned.</param>
/// <param name="VerticalAnchor">What the vertical position is measured from.</param>
/// <param name="VerticalAlignment">How the frame is aligned down.</param>
/// <param name="Y">The offset, when it is not aligned.</param>
/// <param name="Wrap">What text beside the frame does.</param>
/// <param name="Spacing">How far text keeps clear on each side.</param>
public readonly record struct ParagraphFramePosition(
    Length Width,
    Length Height,
    ParagraphFrameHeightRule HeightRule,
    ParagraphFrameRelation HorizontalAnchor,
    ParagraphFrameHorizontalAlignment HorizontalAlignment,
    Length X,
    ParagraphFrameRelation VerticalAnchor,
    ParagraphFrameVerticalAlignment VerticalAlignment,
    Length Y,
    TextWrap Wrap,
    Margins Spacing);

/// <summary>What a paragraph's own properties ask of it: to be framed, or to be a drop cap.</summary>
/// <param name="Position">Where the frame goes.</param>
/// <param name="DropLines">
/// The lines a drop cap spans when the paragraph is one — two when <c>w:lines</c> is unstated or out of
/// range — and zero when it is a frame.
/// </param>
/// <param name="DropDistance">The gap after a drop cap: <c>w:hSpace</c>.</param>
public readonly record struct ParagraphFrameRequest(
    ParagraphFramePosition Position, int DropLines = 0, Length DropDistance = default)
{
    /// <summary>True when the paragraph is a drop cap rather than a frame.</summary>
    public bool IsDropCap => DropLines != 0;
}

/// <summary>
/// Takes a flow's framed paragraphs out of it and turns them into frames, the way writerfilter does for
/// DOCX and RTF.
/// </summary>
/// <remarks>
/// <para>
/// The port of the frame half of <c>DomainMapper_Impl::finishParagraph</c>
/// (<c>DomainMapper_Impl.cxx</c>:2477-2680 on the 26.8 branch) and of
/// <c>CheckUnregisteredFrameConversion</c> (:2085-2140): consecutive framed paragraphs whose resolved
/// properties are equal become one frame, a change of properties starts another, and an unframed
/// paragraph ends the run. The frames hang on the first paragraph left in the flow after them, which is
/// the node <c>convertToTextFrame</c> leaves the anchor at — measured, on the 26.8 daily, by two frames
/// at different offsets after one another: both sit against the top of the paragraph that follows the
/// second. A frame that nothing follows hangs on the paragraph before it.
/// </para>
/// <para>
/// A framed paragraph inside a table cell is left in the cell — <c>CheckUnregisteredFrameConversion</c>
/// returns at once when <c>isInTable()</c> (:2097) — so this is applied to the body and the running
/// heads, and not to a cell's blocks.
/// </para>
/// <para>
/// A drop cap is a framed paragraph too, and is taken out the same way, but it becomes no frame: its text
/// is set as the following paragraph's dropped capital. See <see cref="DropCaps"/>.
/// </para>
/// </remarks>
public static class ParagraphFrames
{
    /// <summary>Lifts a flow's framed paragraphs into frames.</summary>
    /// <param name="blocks">The flow's blocks, in order.</param>
    /// <param name="requestOf">What a paragraph's own properties ask of it, or null when nothing.</param>
    /// <param name="widensForBorderSpacing">
    /// Whether a fixed width grows by the framed paragraphs' left and right border distances — true for
    /// DOCX, false for RTF (<c>lcl_MoveBorderPropertiesToFrame</c>, :1768-1843).
    /// </param>
    /// <param name="breakWidth">
    /// The width the flow's paragraphs are broken at, which a drop cap needs to know how many lines its
    /// paragraph has.
    /// </param>
    /// <returns>The blocks left in the flow, the frames hung on them; the list given when none is framed.</returns>
    public static List<PageBlock> Lift(
        List<PageBlock> blocks,
        Func<PageParagraph, ParagraphFrameRequest?> requestOf,
        bool widensForBorderSpacing,
        Length breakWidth)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(requestOf);

        ParagraphFrameRequest?[] requests =
            [.. blocks.Select(block => block is PageParagraph paragraph ? requestOf(paragraph) : null)];
        if (requests.All(request => request is null)) return blocks;

        List<PageBlock> kept = new(blocks.Count);
        List<PageFrame> pending = [];
        (PageParagraph Letters, ParagraphFrameRequest Request)? drop = null;

        for (int index = 0; index < blocks.Count; index++)
        {
            PageBlock block = blocks[index];

            if (requests[index] is { IsDropCap: true } dropRequest)
            {
                // A second drop cap before the first found its paragraph: the first is laid out as the
                // ordinary paragraph it is, which is what writerfilter's "must not occur" leaves.
                if (drop is { } stranded) kept.Add(stranded.Letters);
                drop = ((PageParagraph)block, dropRequest);
                continue;
            }

            if (requests[index] is { } frameRequest)
            {
                List<PageParagraph> inside = [];
                while (index < blocks.Count
                       && blocks[index] is PageParagraph framed
                       && requests[index] is { IsDropCap: false } same
                       && same.Position == frameRequest.Position)
                {
                    inside.Add(framed);
                    index++;
                }

                index--;

                // A pending drop cap belongs to the first framed paragraph (case 5).
                if (drop is { } letters)
                {
                    inside[0] = DropCaps.Apply(
                        inside[0], letters.Letters, letters.Request.DropLines,
                        letters.Request.DropDistance, frameRequest.Position.Width > Length.Zero
                            ? frameRequest.Position.Width
                            : breakWidth,
                        resetsSize: true);
                    drop = null;
                }

                // A page break the framed paragraph's style asks for is made an empty paragraph of its own
                // ahead of the frame (`DomainMapper::HandleFramedParagraphPageBreak`, :1635-1675), since a
                // break inside a frame breaks nothing.
                if (inside[0].Format.StartsNewPage) kept.Add(PageBreakParagraph(inside[0]));

                pending.Add(Build(frameRequest.Position, inside, widensForBorderSpacing));
                continue;
            }

            if (block is PageParagraph paragraph)
            {
                if (drop is { } letters)
                {
                    paragraph = DropCaps.Apply(
                        paragraph, letters.Letters, letters.Request.DropLines,
                        letters.Request.DropDistance, breakWidth, resetsSize: true);
                    drop = null;
                }

                kept.Add(Anchoring(paragraph, pending));
                continue;
            }

            if (drop is { } beforeTable)
            {
                kept.Add(beforeTable.Letters);
                drop = null;
            }

            // A table cannot carry an anchor here, so a frame written just before one hangs on the
            // paragraph before that — see the remarks on the class for what writerfilter does instead.
            if (pending.Count > 0) HangOnLastParagraph(kept, pending);
            kept.Add(block);
        }

        if (drop is { } trailing) kept.Add(trailing.Letters);
        if (pending.Count > 0) HangOnLastParagraph(kept, pending);
        return kept;
    }

    /// <summary>The paragraph with the pending frames anchored on it.</summary>
    private static PageParagraph Anchoring(PageParagraph paragraph, List<PageFrame> pending)
    {
        if (pending.Count == 0) return paragraph;

        PageParagraph anchored = paragraph with { Frames = [.. pending, .. paragraph.Frames] };
        pending.Clear();
        return anchored;
    }

    /// <summary>Hangs pending frames on the last paragraph kept, or drops them when there is none.</summary>
    private static void HangOnLastParagraph(List<PageBlock> kept, List<PageFrame> pending)
    {
        for (int index = kept.Count - 1; index >= 0; index--)
        {
            if (kept[index] is not PageParagraph paragraph) continue;
            kept[index] = paragraph with { Frames = [.. paragraph.Frames, .. pending] };
            break;
        }

        pending.Clear();
    }

    /// <summary>The empty paragraph a framed paragraph's page break is moved onto.</summary>
    private static PageParagraph PageBreakParagraph(PageParagraph framed)
        => framed with
        {
            Text = "",
            Runs = [],
            Notes = [],
            Frames = [],
            Fields = [],
            Anchors = [],
            Label = null,
            Borders = null,
            Shading = null,
            Format = framed.Format with { SpaceBefore = Length.Zero, SpaceAfter = Length.Zero },
        };

    /// <summary>
    /// The frame one run of framed paragraphs makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The paragraphs' borders become the frame's</strong> (<c>lcl_MoveBorderPropertiesToFrame</c>):
    /// the four lines are cleared from every paragraph and set on the frame, and for DOCX the frame's fixed
    /// width grows by the left and right distances. Measured on the 26.8 daily, a 144 pt frame round a
    /// paragraph with 1 pt rules 4 pt clear of the text is drawn 152 pt wide with its text 5 pt inside each
    /// edge, and an RTF one stays 144 pt wide with the same 5 pt inset. That geometry is exactly the
    /// paragraph's own border drawn inside a frame padded by each side's allowance, so the paragraphs keep
    /// their borders and the frame takes the padding.
    /// </para>
    /// <para>
    /// The borders are read off the whole run at once, and a cursor over paragraphs that disagree answers
    /// the pool default — so a run whose paragraphs are not all bordered alike has no border at all, which
    /// the daily confirms: one bordered paragraph above an unbordered one in a frame draws no rule.
    /// </para>
    /// <para>
    /// The frame itself is transparent (<c>BackColorTransparency</c> 100), so a paragraph's shading is the
    /// only fill and fills the paragraph's own width, which is the frame's.
    /// </para>
    /// </remarks>
    /// <param name="position">Where the frame goes.</param>
    /// <param name="paragraphs">The paragraphs it holds, in order.</param>
    /// <param name="widensForBorderSpacing">See <see cref="Lift"/>.</param>
    public static PageFrame Build(
        ParagraphFramePosition position, IReadOnlyList<PageParagraph> paragraphs, bool widensForBorderSpacing)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);

        ParagraphBorderSet? borders = paragraphs.Count > 0 && paragraphs[0].Borders is { IsEmpty: false } first
            && paragraphs.All(paragraph => Equals(paragraph.Borders, first))
                ? first
                : null;

        List<PageBlock> blocks = [.. paragraphs.Select(paragraph => (PageBlock)(paragraph with
        {
            Borders = borders is null ? null : paragraph.Borders,
            Format = paragraph.Format.StartsNewPage
                ? paragraph.Format with { StartsNewPage = false }
                : paragraph.Format,
        }))];

        ParagraphBorderJoin.Apply(blocks);

        Length padLeft = borders?.Left?.Allowance ?? Length.Zero;
        Length padRight = borders?.Right?.Allowance ?? Length.Zero;
        Length widened = widensForBorderSpacing && borders is not null
            ? (borders.Left?.Space ?? Length.Zero) + (borders.Right?.Space ?? Length.Zero)
            : Length.Zero;

        bool automatic = position.Width <= Length.Zero;

        return new PageFrame
        {
            Size = new DocSize(
                automatic ? padLeft + padRight : position.Width + widened,
                position.HeightRule == ParagraphFrameHeightRule.Auto ? Length.Zero : position.Height),
            Anchor = FrameAnchor.Paragraph,
            Wrap = position.Wrap,
            HorizontalOrigin = position.HorizontalAnchor switch
            {
                ParagraphFrameRelation.Page => FrameHorizontalOrigin.Page,
                ParagraphFrameRelation.Margin => FrameHorizontalOrigin.PageMargin,
                _ => FrameHorizontalOrigin.Column,
            },

            // INSIDE and OUTSIDE reach `CalcRelPosX` as neither CENTER nor RIGHT, so they take the left
            // arm (`anchoredobjectposition.cxx`:996-1005) — a paragraph frame never toggles its position.
            // Measured: both are drawn flush left on a right-hand page.
            HorizontalAlignment = position.HorizontalAlignment switch
            {
                ParagraphFrameHorizontalAlignment.None => FrameHorizontalAlignment.Offset,
                ParagraphFrameHorizontalAlignment.Centre => FrameHorizontalAlignment.Centre,
                ParagraphFrameHorizontalAlignment.Right => FrameHorizontalAlignment.Right,
                _ => FrameHorizontalAlignment.Left,
            },
            HorizontalOffset = position.X,
            VerticalOrigin = position.VerticalAnchor switch
            {
                ParagraphFrameRelation.Page => FrameVerticalOrigin.Page,
                ParagraphFrameRelation.Margin => FrameVerticalOrigin.PageMargin,
                _ => FrameVerticalOrigin.Paragraph,
            },
            VerticalAlignment = position.VerticalAlignment switch
            {
                ParagraphFrameVerticalAlignment.Top => FrameVerticalAlignment.Top,
                ParagraphFrameVerticalAlignment.Centre => FrameVerticalAlignment.Middle,
                ParagraphFrameVerticalAlignment.Bottom => FrameVerticalAlignment.Bottom,
                _ => FrameVerticalAlignment.Offset,
            },
            VerticalOffset = position.Y,
            Spacing = position.Spacing,
            Padding = new Margins(padLeft, Length.Zero, padRight, Length.Zero),
            Blocks = blocks,
            HasFixedHeight = position.HeightRule == ParagraphFrameHeightRule.Exact,
            GrowsToContent = position.HeightRule != ParagraphFrameHeightRule.Exact,
            HeightFloor = position.HeightRule == ParagraphFrameHeightRule.Auto ? Length.Zero : null,
            WidthFromContent = automatic,
        };
    }
}
