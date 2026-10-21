using XRay.Office.Core.Geometry;
using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;
using XRay.Office.Text.Layout;

namespace XRay.Office.WordProcessing.Layout;

/// <summary>
/// Sets a paragraph's first letters as a dropped capital, the way Writer's <c>SwDropPortion</c> does.
/// </summary>
/// <remarks>
/// <para>
/// <strong>How Writer sizes it</strong> (<c>sw/source/core/text/txtdrop.cxx</c>): the drop is as tall as
/// the distance from the top of the paragraph's first line to the baseline of its <em>N</em>th —
/// <c>CalcDropHeight</c> sums the first <em>N</em> − 1 lines' heights and adds the <em>N</em>th's ascent
/// (:482-542) — and <c>SwDropCapCache::CalcFontSize</c> (:714-992) then searches for the font scale at
/// which the union of the letters' glyph boxes is exactly that tall, by
/// <c>factor = factor × wished / measured</c> in thousandths, keeping the last factor once it stops
/// moving between the two it has bracketed. The letters are drawn on the <em>N</em>th line's baseline;
/// the lines beside them start where the drop's advance, plus its distance, ends — and for lines 2 to
/// <em>N</em> that is measured from the <em>first-line</em> indent (<c>itrtxt.hxx</c>:335), not the
/// left one.
/// </para>
/// <para>
/// <strong>Measured on the 26.8 daily</strong>, DOCX and ODT alike (<c>probes/xo-frames</c>): a three-line
/// <c>W</c> in 12 pt Liberation Serif is drawn at 57.9 pt with its baseline on the third line's, and the
/// three lines start 54.6 pt along — its advance at that size. Two lines draw it at 37.3 pt, four at 78.5,
/// one line not at all (the letter stays in the text at its own size), and a paragraph of a single line
/// likewise; a paragraph of two lines under a three-line drop gets a two-line one, because
/// <c>CalcDropHeight</c> stops at the last line there is. The face's glyph boxes reproduce the size to
/// within 0.6 pt for the serif letters measured (<c>W</c>, <c>WA</c>, <c>“W</c>, <c>J</c>, <c>O</c>) and
/// 1.3 pt for a sans <c>W</c>: the reference measures rendered glyph bounds
/// (<c>OutputDevice::GetTextBoundRect</c>), which a hinted rasteriser rounds outward, and the design
/// box is what this has.
/// </para>
/// <para>
/// <strong>How it is laid out here.</strong> The letters leave the paragraph's text and become two
/// frames anchored on it: one that wraps nothing and draws them at the scaled size on the <em>N</em>th
/// baseline, and an empty one as wide as the drop that the first <em>N</em> lines flow round on its right.
/// So nothing in the paginator or the line breaker knows about drop caps, and the rest of the paragraph
/// keeps every offset it had. The geometry is decided once, here, against the paragraph laid out at the
/// width it will be broken at — which is Writer's own first guess and only differs from its second when
/// the drop changes a line's height.
/// </para>
/// </remarks>
public static class DropCaps
{
    /// <summary>Lines 2 to <em>N</em> start one twip further than the obstacle, so it is a twip narrower.</summary>
    private static readonly Length Inflation = Length.FromTwips(1);

    /// <summary>
    /// The paragraph with a separate paragraph's text set as its dropped capital.
    /// </summary>
    /// <param name="host">The paragraph the letters drop into.</param>
    /// <param name="letters">
    /// The letters, as a paragraph of their own — which is how DOCX and DOC write them: a framed paragraph
    /// holding nothing else, ahead of the one it belongs to.
    /// </param>
    /// <param name="lines">The lines the drop spans; fewer than two sets the letters in the text instead.</param>
    /// <param name="distance">The gap after the drop.</param>
    /// <param name="breakWidth">The width the paragraph is broken at.</param>
    /// <param name="resetsSize">
    /// True when every letter is first set back to the paragraph's own size, as writerfilter and the WW8
    /// filter both do (<c>setPropertyToDefault(PROP_CHAR_HEIGHT)</c>, <c>DomainMapper_Impl.cxx</c>:2516;
    /// <c>m_xCtrlStck-&gt;Delete</c>, <c>ww8par.cxx</c>:4011-4015). It decides nothing but the size the
    /// scale starts from and the height the first line is given for the drop.
    /// </param>
    public static PageParagraph Apply(
        PageParagraph host,
        PageParagraph letters,
        int lines,
        Length distance,
        Length breakWidth,
        bool resetsSize)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(letters);

        if (letters.Text.Length == 0) return host;

        PageParagraph prepared = resetsSize ? WithSize(letters, host.EmSize) : letters;
        return Drop(host, prepared, lines, distance, breakWidth) ?? Prepend(prepared, host);
    }

    /// <summary>
    /// The paragraph with its own first characters set as its dropped capital — ODF's
    /// <c>style:drop-cap</c>.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="characters">How many characters drop; zero with <paramref name="wholeWord"/> for a word.</param>
    /// <param name="wholeWord">True for <c>style:length="word"</c>.</param>
    /// <param name="lines">The lines the drop spans.</param>
    /// <param name="distance">The gap after the drop.</param>
    /// <param name="breakWidth">The width the paragraph is broken at.</param>
    /// <param name="style">How a character style the drop names restyles a run, or null for none.</param>
    /// <param name="punctuation">
    /// The document's <c>DropCapPunctuation</c> setting, which measures a drop's ink from the baseline;
    /// Writer applies it only to a drop of a counted number of characters, never to a whole word
    /// (<c>txtdrop.cxx</c>:800-802).
    /// </param>
    public static PageParagraph ApplyPrefix(
        PageParagraph paragraph,
        int characters,
        bool wholeWord,
        int lines,
        Length distance,
        Length breakWidth,
        Func<PageRun, PageRun>? style = null,
        bool punctuation = true)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (lines < 2) return paragraph;

        int length = DropLength(paragraph.Text, wholeWord ? 0 : characters);
        if (length <= 0) return paragraph;

        (PageParagraph head, PageParagraph tail) = Split(paragraph, length);
        if (style is not null) head = head with { Runs = [.. RunsOf(head).Select(style)] };

        return Drop(tail, head, lines, distance, breakWidth, punctuation && !wholeWord) ?? paragraph;
    }

    /// <summary>
    /// How many characters a drop takes: <c>SwTextFrame::GetDropLen</c> (<c>txtdrop.cxx</c>:170-227),
    /// the wished count or the first word, cut at the first tab or line break.
    /// </summary>
    internal static int DropLength(string text, int wished)
    {
        int end = text.Length;
        if (wished > 0 && wished < end) end = wished;

        if (wished <= 0)
        {
            int word = 0;
            while (word < text.Length && char.IsLetterOrDigit(text, word)) word++;
            end = word;
        }

        int index = 0;
        while (index < end && text[index] is not ('\t' or '\n' or '\u2028' or '\u000B')) index++;
        return index;
    }

    /// <summary>The two frames that draw a drop and make room for it, or null when it does not drop.</summary>
    private static PageParagraph? Drop(
        PageParagraph host,
        PageParagraph letters,
        int lines,
        Length distance,
        Length breakWidth,
        bool keepsBaseline = true)
    {
        if (lines < 2 || breakWidth <= Length.Zero) return null;

        LaidOutParagraph laid = LayOut(host, breakWidth);
        List<LineBox> rows = [.. laid.Lines.Where(box => !box.SharesLineWithNext)];

        // A paragraph of one line is set without a drop at all, and one shorter than the drop gets a drop
        // as tall as it is (`CalcDropHeight`, :501-525).
        int spanned = Math.Min(lines, rows.Count);
        if (spanned < 2) return null;

        // Measured from the first line as the paginator places it: a paragraph's first line gives up the
        // leading proportional spacing puts above it (`ParagraphLeading.AsDrawn`), and every line below
        // moves up with it, so both ends are taken relative to the drawn line's top.
        LineBox first = rows[0];
        LineBox drawnFirst = ParagraphLeading.AsDrawn(
            first, isFirstOfParagraph: true, isFirstInFrame: false, host.Format.LineSpacing);
        Length dropped = first.Height - drawnFirst.Height;
        LineBox last = rows[spanned - 1];
        Length textTop = drawnFirst.SpaceAbove;
        Length baseline = spanned == 1
            ? drawnFirst.Baseline
            : last.Top - first.Top - dropped + last.Baseline;
        long wished = (baseline - textTop).Twips;
        if (wished <= 0) return null;

        List<PageRun> runs = RunsOf(letters);
        long factor = Factor(letters.Text, runs, wished, keepsBaseline, out long descent);
        if (factor <= 0) return null;

        List<PageRun> scaled = [.. runs.Select(run => run with
        {
            EmSize = Scaled(run.EmSize, factor),
            MetricEmSize = run.MetricEmSize == Length.Zero ? Length.Zero : Scaled(run.MetricEmSize, factor),
            Rise = Length.Zero,
        })];

        Length advance = Length.Zero;
        foreach (PageRun run in scaled)
        {
            advance += new TextMeasurer(run.Face).Measure(
                letters.Text.AsSpan(run.Start, run.Length), run.EmSize, run.EffectiveShaping);
        }

        // The distance is given up when it would not fit beside the drop (`SwDropPortion::Format`,
        // :1077-1087).
        Length lineStart = host.Format.LineStart(true);
        Length room = host.Format.FirstLineWidth(breakWidth);
        Length width = advance + distance <= room ? advance + distance : advance;

        PageParagraph drawn = letters with
        {
            Runs = scaled,
            EmSize = scaled[0].EmSize,
            Face = scaled[0].Face,
            Font = scaled[0].Font,
            Format = ParagraphFormat.Default,
            Label = null,
            Borders = null,
            Shading = null,
            Notes = [],
            Frames = [],
            Fields = [],
            Anchors = [],
            Source = null,
        };

        IReadOnlyList<LineBox> ownLines = LayOut(drawn, advance + Length.FromPoints(72)).Lines;
        LineBox own = ownLines.Count > 0 ? ownLines[0] : default;
        Length ownHeight = own.Height > Length.Zero ? own.Height : Length.FromTwips(wished);

        PageFrame letterFrame = new()
        {
            Size = new DocSize(advance + Length.FromPoints(1), ownHeight),
            Anchor = FrameAnchor.Paragraph,
            Wrap = TextWrap.Through,
            HorizontalOrigin = FrameHorizontalOrigin.Column,
            HorizontalOffset = lineStart,
            VerticalOrigin = FrameVerticalOrigin.Line,
            // `CalcFontSize` moves the letters by their ink's descent (`pDrop->SetY`, :991), so their ink
            // ends on the drop's baseline: nothing under the punctuation rule, which measured from the
            // baseline, and a word's descending ink raised by its depth.
            VerticalOffset = baseline - own.Baseline - Length.FromTwips(descent),
            Blocks = [drawn],
            Name = "drop cap",
        };

        PageFrame roomFrame = new()
        {
            Size = new DocSize(Length.Max(Length.Zero, width - Inflation), baseline),
            Anchor = FrameAnchor.Paragraph,
            Wrap = TextWrap.Right,
            HorizontalOrigin = FrameHorizontalOrigin.Column,
            HorizontalOffset = lineStart,
            VerticalOrigin = FrameVerticalOrigin.Line,
            VerticalOffset = Length.Zero,
            Name = "drop cap room",
        };

        return host with { Frames = [letterFrame, roomFrame, .. host.Frames] };
    }

    /// <summary>
    /// The scale, in thousandths, at which the letters' ink is <paramref name="wished"/> twips tall —
    /// <c>SwDropCapCache::CalcFontSize</c>'s search, with the face's glyph boxes for the bound rectangle.
    /// </summary>
    /// <param name="text">The letters.</param>
    /// <param name="runs">Their runs, at the size the scale starts from.</param>
    /// <param name="wished">The height, in twips, the ink is to fill.</param>
    /// <param name="keepsBaseline">True under the drop-cap punctuation rule.</param>
    /// <param name="descent">
    /// How far, in twips, the ink last measured reaches below the baseline; negative when it ends above it.
    /// </param>
    internal static long Factor(
        string text, IReadOnlyList<PageRun> runs, long wished, bool keepsBaseline, out long descent)
    {
        descent = 0;
        long maxHeight = 1;
        foreach (PageRun run in runs) maxHeight = Math.Max(maxHeight, run.EmSize.Twips);

        long factor = 1000 * wished / maxHeight;
        long min = 0;
        long max = long.MaxValue;

        for (int attempt = 0; attempt < 64; attempt++)
        {
            double above = double.NegativeInfinity;
            double below = double.NegativeInfinity;

            foreach (PageRun run in runs)
            {
                double size = factor * run.EmSize.Twips / 1000;
                double upem = Math.Max(1, run.Face.UnitsPerEm);
                double partAbove = double.NegativeInfinity;
                double partBelow = double.NegativeInfinity;

                for (int index = run.Start; index < run.End && index < text.Length; index++)
                {
                    int codePoint = char.IsSurrogatePair(text, index) ? char.ConvertToUtf32(text, index++) : text[index];
                    ushort glyph = run.Face.Characters.GlyphFor(codePoint);

                    // A face with no glyph boxes is measured by its ascent, which is Writer's own fallback
                    // when the bound rectangle cannot be had (:855-859). A glyph with no contours adds no
                    // ink to the bound rectangle.
                    (int Top, int Bottom) box = GlyphInkExtents.Box(run.Face, glyph)
                        ?? (run.Face.Horizontal.Ascender, 0);
                    if (box is (0, 0)) continue;

                    partAbove = Math.Max(partAbove, box.Top * size / upem);
                    partBelow = Math.Max(partBelow, -box.Bottom * size / upem);
                }

                if (double.IsNegativeInfinity(partAbove)) continue;

                // With the punctuation rule on, a portion whose ink rises above the baseline is measured
                // from the baseline, plus a sixtieth of its ascent (:864-868, tdf#150200): a quotation
                // mark or a dash then drops at a letter's size instead of being blown up to fill the
                // height on its own. The rule is what DOCX, DOC and every new ODF document get.
                if (keepsBaseline && partAbove > 0)
                {
                    partBelow = 0;
                    partAbove += run.Face.Horizontal.Ascender * size / upem / 60;
                }

                above = Math.Max(above, partAbove);
                below = Math.Max(below, partBelow);
            }

            if (double.IsNegativeInfinity(above)) break;

            long measured = (long)Math.Round(above + below);
            if (measured <= 0) break;

            descent = (long)Math.Round(below);

            if (measured > wished) max = factor;
            else min = factor;

            long next = factor * wished / measured;
            bool grows = next > min && next < max;
            factor = next;
            if (!grows) break;
        }

        return factor;
    }

    private static Length Scaled(Length size, long factor) => Length.FromTwips(factor * size.Twips / 1000);

    /// <summary>A paragraph laid out at a width, by the same route the paginator takes.</summary>
    private static LaidOutParagraph LayOut(PageParagraph paragraph, Length width)
    {
        ParagraphLayouter layouter = new(
            paragraph.Face, breaker: null, paragraph.Metrics, WriterLineBox.LeadingAboveText);

        return paragraph.HasRuns || paragraph.HasInlineObjects || paragraph.LabelRaisesFirstLine
               || paragraph.NeedsGlyphFallback || paragraph.HasScriptSpace || paragraph.IsHorizontallyScaled
            ? layouter.Layout(
                paragraph.Measure(), paragraph.Format, width, paragraph.Language, null, null, paragraph.EmSize)
            : layouter.Layout(
                paragraph.Text, paragraph.Format, paragraph.EmSize, width, paragraph.Language, null,
                paragraph.EffectiveShaping);
    }

    /// <summary>A paragraph's runs, or one run standing for its uniform formatting when it has none.</summary>
    internal static List<PageRun> RunsOf(PageParagraph paragraph)
        => paragraph.HasRuns
            ? [.. paragraph.Runs]
            : [new PageRun(
                0, paragraph.Text.Length, paragraph.Face, paragraph.EmSize, paragraph.Font, paragraph.Colour,
                paragraph.Shaping, Tracking: paragraph.Tracking, WidthPerCent: paragraph.WidthPerCent,
                Item: paragraph.Item)];

    /// <summary>Every run of the letters set at one size, with no rise.</summary>
    private static PageParagraph WithSize(PageParagraph letters, Length size)
        => letters with
        {
            EmSize = size,
            Runs = [.. RunsOf(letters).Select(run => run with
            {
                EmSize = size, MetricEmSize = Length.Zero, Rise = Length.Zero,
            })],
        };

    /// <summary>
    /// One paragraph's text and runs set in front of another's, which is what a drop cap that does not
    /// drop leaves: writerfilter never finished the letters' paragraph, so they are the start of the next.
    /// </summary>
    internal static PageParagraph Prepend(PageParagraph head, PageParagraph body)
    {
        int shift = head.Text.Length;
        if (shift == 0) return body;

        return body with
        {
            Text = head.Text + body.Text,
            Runs = [.. RunsOf(head), .. RunsOf(body).Select(run => run with { Start = run.Start + shift })],
            Notes = [.. body.Notes.Select(note => note with { Offset = note.Offset + shift })],
            Frames = [.. body.Frames.Select(frame => frame with { AnchorOffset = frame.AnchorOffset + shift })],
            Fields = [.. body.Fields.Select(field => field with { Start = field.Start + shift })],
            Anchors = [.. body.Anchors.Select(anchor => anchor with { Offset = anchor.Offset + shift })],
        };
    }

    /// <summary>A paragraph cut in two after its first <paramref name="length"/> characters.</summary>
    /// <remarks>
    /// The head keeps only text and runs, since a drop draws nothing else; the tail keeps everything that
    /// sits past the cut, moved back by it, and what sat inside the cut is moved to its start.
    /// </remarks>
    internal static (PageParagraph Head, PageParagraph Tail) Split(PageParagraph paragraph, int length)
    {
        List<PageRun> runs = RunsOf(paragraph);

        List<PageRun> head = [];
        List<PageRun> tail = [];
        foreach (PageRun run in runs)
        {
            int start = run.Start;
            int end = run.End;
            if (start < length) head.Add(run with { Length = Math.Min(end, length) - start });
            if (end > length)
            {
                int from = Math.Max(start, length);
                tail.Add(run with { Start = from - length, Length = end - from });
            }
        }

        int Moved(int offset) => Math.Max(0, offset - length);

        PageParagraph headParagraph = paragraph with { Text = paragraph.Text[..length], Runs = head };
        PageParagraph tailParagraph = paragraph with
        {
            Text = paragraph.Text[length..],
            Runs = paragraph.HasRuns ? tail : [],
            Notes = [.. paragraph.Notes.Select(note => note with { Offset = Moved(note.Offset) })],
            Frames = [.. paragraph.Frames.Select(frame => frame with { AnchorOffset = Moved(frame.AnchorOffset) })],
            Fields = [.. paragraph.Fields
                .Where(field => field.End > length)
                .Select(field => field with { Start = Moved(field.Start), Length = field.End - Math.Max(field.Start, length) })],
            Anchors = [.. paragraph.Anchors.Select(anchor => anchor with { Offset = Moved(anchor.Offset) })],
        };

        return (headParagraph, tailParagraph);
    }
}
