using XRay.Office.Core.Globalization;
using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;
using XRay.Office.Text.Shaping;
using XRay.Office.Core.Threading;

namespace XRay.Office.Text.Layout;

/// <summary>
/// Measures text by shaping it.
/// </summary>
/// <remarks>
/// <para>
/// The sum of the glyphs' advances, scaled once from the face's design grid to the requested em size.
/// That is what decides where a line runs past the margin, so it decides where the line breaks — and a
/// break in a different place moves everything after it, so this measurement compounds down the page
/// rather than staying local.
/// </para>
/// <para>
/// Measuring means shaping, not adding up the font's advance widths. The difference is kerning and
/// ligatures, and it is larger than it sounds: a line of English prose at 12 pt in Carlito accumulates
/// about a quarter of an em of kerning, roughly three points, which is enough to decide whether the
/// line's last word fits. Skipping it does not produce slightly-different-looking output — it produces
/// a different set of line breaks from Writer's, and then a different pagination.
/// </para>
/// <para>
/// Callers measuring the same text repeatedly — which is what filling lines does — should shape once
/// with <see cref="Shape"/> and ask the result for prefix widths, rather than calling
/// <see cref="Measure"/> per candidate. Shaping is contextual as well as expensive, so measuring a
/// prefix on its own is both slower and, for a face with contextual features, not quite the same
/// number.
/// </para>
/// </remarks>
public sealed class TextMeasurer
{
    private readonly OpenTypeFace _face;
    private readonly ITextShaper _shaper;

    /// <summary>Creates a measurer over a face, shaping with the default shaper.</summary>
    public TextMeasurer(OpenTypeFace face)
        : this(face, TextShaper.Default)
    {
    }

    /// <summary>Creates a measurer over a face and a shaper.</summary>
    public TextMeasurer(OpenTypeFace face, ITextShaper shaper)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(shaper);
        _face = face;
        _shaper = shaper;
    }

    /// <summary>The face being measured with.</summary>
    public OpenTypeFace Face => _face;

    /// <summary>The shaper being measured with.</summary>
    public ITextShaper Shaper => _shaper;

    /// <summary>Shapes a run, giving something that can answer for every prefix of it.</summary>
    public ShapedText Shape(ReadOnlySpan<char> text, ShapingOptions options = default)
        => _shaper.Shape(_face, text, options);

    /// <summary>The width of a run of text at an em size.</summary>
    public Length Measure(ReadOnlySpan<char> text, Length emSize, ShapingOptions options = default)
        => Shape(text, options).Width(emSize);

    /// <summary>The width of a run of text in the face's design units.</summary>
    public long MeasureInDesignUnits(ReadOnlySpan<char> text, ShapingOptions options = default)
        => Shape(text, options).AdvanceInDesignUnits;

    /// <summary>Scales a measurement in design units to an em size.</summary>
    public Length Scale(long designUnits, Length emSize)
        => _face.UnitsPerEm <= 0
            ? Length.Zero
            : Length.FromEmu((long)Math.Round((double)designUnits * emSize.Emu / _face.UnitsPerEm));
}

/// <summary>One line of a laid-out paragraph.</summary>
/// <param name="Start">The line's first character, as an index into the paragraph's text.</param>
/// <param name="End">One past its last character, trailing spaces included.</param>
/// <param name="VisibleEnd">
/// One past its last character excluding the trailing spaces. The two differ because a line's trailing
/// spaces are not part of its width — they hang past the margin rather than pushing a word to the next
/// line, which is why a paragraph of "a b c" does not break after every space.
/// </param>
/// <param name="Width">The width of the line's visible text.</param>
/// <param name="EndsParagraph">True when this is the paragraph's last line.</param>
/// <param name="ShrinkAllowance">
/// How much wider than its room the line was allowed to be, because a justified line's blanks may be
/// squeezed below their natural width. Zero for every line that was filled the ordinary way.
/// <para>
/// It travels with the line because the filler is the only thing that knows it — the room a line was
/// filled to, and the natural width of the blanks on it — and whatever justifies the line afterwards
/// needs the same number to know how far it may squeeze. Recomputing it there would mean measuring the
/// blanks a second time, and a second measurement that disagreed by a twip would put the line's last word
/// past the margin. See <see cref="JustificationShrink"/>.
/// </para>
/// </param>
/// <param name="Hyphenated">
/// True when the line ends inside a word that automatic hyphenation broke, so a hyphen is drawn after
/// <paramref name="VisibleEnd"/>. <paramref name="Width"/> includes the hyphen, as Writer's line includes its
/// <c>SwHyphPortion</c>, so alignment and justification see the line the hyphen ends.
/// </param>
public readonly record struct TextLine(
    int Start,
    int End,
    int VisibleEnd,
    Length Width,
    bool EndsParagraph,
    Length ShrinkAllowance = default,
    bool Hyphenated = false)
{
    /// <summary>How many characters the line spans, trailing spaces included.</summary>
    public int Length => End - Start;

    /// <summary>The line's text, given the paragraph's.</summary>
    public ReadOnlySpan<char> TextIn(ReadOnlySpan<char> paragraph)
        => paragraph[Start..Math.Min(End, paragraph.Length)];

    /// <summary>The line's visible text, without the trailing spaces.</summary>
    public ReadOnlySpan<char> VisibleTextIn(ReadOnlySpan<char> paragraph)
        => paragraph[Start..Math.Min(VisibleEnd, paragraph.Length)];
}

/// <summary>
/// Fills lines to a width, the way a word processor does.
/// </summary>
/// <remarks>
/// <para>
/// Greedy, one line at a time: take break opportunities until the next one would overflow, then break
/// at the last that fit. That is what Writer does, and it is deliberately <em>not</em> Knuth-Plass —
/// the total-fit algorithm produces better-looking paragraphs and different line breaks, and different
/// line breaks are precisely what must not happen when the point is to agree with LibreOffice.
/// </para>
/// <para>
/// Two details decide more breaks than the measurement does. A line's trailing spaces do not count
/// towards its width, so a space that would overflow the margin does not push its word to the next
/// line. And a single word too long for the line is not left to overflow forever: it takes the line
/// alone and is allowed to exceed it, because the alternative is an empty line followed by the same
/// problem.
/// </para>
/// <para>
/// <strong>The first of those two is Writer's rule and not EditEngine's</strong>, which is why
/// <see cref="LineFiller(TextMeasurer, ILineBreaker, bool)"/> can turn it off. See
/// <see cref="BreaksOverflowingBlanks"/>.
/// </para>
/// <para>
/// The paragraph is shaped once and every candidate width read off the result. Shaping each candidate
/// would be quadratic in the paragraph's length, and would also answer slightly differently, since a
/// prefix shaped alone is not always a prefix of the shaped whole.
/// </para>
/// </remarks>
public sealed class LineFiller
{
    private readonly TextMeasurer _measurer;
    private readonly ILineBreaker _breaker;
    private readonly bool _breaksOverflowingBlanks;

    /// <summary>Creates a filler over a measurer and a break iterator.</summary>
    /// <param name="measurer">The measurer the candidate widths are read from.</param>
    /// <param name="breaker">The break iterator, or null for the default.</param>
    /// <param name="breaksOverflowingBlanks">
    /// Whether a run of blanks wider than the line is broken inside rather than allowed to hang past
    /// it. See <see cref="BreaksOverflowingBlanks"/>; false is Writer's rule and the default.
    /// </param>
    public LineFiller(
        TextMeasurer measurer, ILineBreaker? breaker = null, bool breaksOverflowingBlanks = false)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
        _breaker = breaker ?? LineBreaker.Instance;
        _breaksOverflowingBlanks = breaksOverflowingBlanks;
    }

    /// <summary>
    /// Whether a line's trailing blanks are allowed to overflow it, or break inside instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A word processor lets them hang: a line's trailing spaces do not count towards its width, so a
    /// run of twenty blanks at the end of a line is free however narrow the column is. That is Writer,
    /// and it is what this class does by default.
    /// </para>
    /// <para>
    /// <strong>EditEngine — which is what lays out a Calc cell — does not.</strong>
    /// <c>ImpEditEngine::ImpBreakLine</c> walks the character-position array while it is under the
    /// remaining width and the array holds every character's advance, blanks included
    /// (<c>editeng/source/editeng/impedit3.cxx:2016-2018</c>). When the character it stops on is a
    /// blank it breaks immediately behind it and compresses it —
    /// "Break behind the blank, blank will be compressed" (<c>:2031-2035</c>) — so a run of blanks is
    /// cut into line-fuls rather than hanging off the end of one line.
    /// </para>
    /// <para>
    /// Measured on <c>dotnet/probes/sheets-rest-01/mkspaceprobe.py</c> under the installed 26.2.4.2,
    /// eighteen wrapped cells differing only in their trailing whitespace: a cell holding 40 blanks in
    /// a 71.25 pt column takes two lines, 100 blanks take four, 160 take five and 200 take seven, and
    /// all eighteen are reproduced exactly by breaking behind the blank that overflows. Without this
    /// every one of them is one line.
    /// </para>
    /// </remarks>
    public bool BreaksOverflowingBlanks => _breaksOverflowingBlanks;

    /// <summary>
    /// Breaks a paragraph measured across its runs into lines that fit a width.
    /// </summary>
    /// <remarks>
    /// The same greedy walk as the single-face overload, differing only in where a candidate's width comes
    /// from — a <see cref="MeasuredParagraph"/> answers across runs where a <see cref="TextMeasurer"/>
    /// answers for one face. Keeping one algorithm matters more than the sharing: two fillers would break
    /// lines differently the first time one was changed.
    /// </remarks>
    /// <param name="measured">The paragraph, already shaped run by run.</param>
    /// <param name="availableWidth">The width a line has to fit in.</param>
    /// <param name="firstLineWidth">The width available to the first line, when it differs.</param>
    /// <param name="language">A BCP 47 tag, for the language-specific break rules.</param>
    /// <param name="tabs">
    /// The paragraph's format, or null. Two things are read from it: its tab stops, consulted only for a
    /// line that holds a tab, and whether a justified line may squeeze its blanks
    /// (<see cref="ParagraphFormat.ShrinksJustifiedBlanks"/>). A paragraph that is neither tabbed nor
    /// justified measures exactly as it would without this parameter.
    /// </param>
    /// <param name="widthOfLine">
    /// The width one line may use, asked once per line just before it is filled. Null for the ordinary
    /// case, where every line but the first gets the same width; it exists for text flowing round a
    /// floating frame, where the width depends on how far down the paragraph the line sits.
    /// </param>
    /// <param name="cellBroken">
    /// The stretches that break between characters rather than between words, or null when there are
    /// none — which is every paragraph a word processor lays out. See <see cref="CellBrokenSpan"/>.
    /// </param>
    public List<TextLine> Fill(
        MeasuredParagraph measured,
        Length availableWidth,
        Length? firstLineWidth = null,
        string? language = null,
        ParagraphFormat? tabs = null,
        Func<int, IReadOnlyList<TextLine>, Length>? widthOfLine = null,
        IReadOnlyList<CellBrokenSpan>? cellBroken = null)
    {
        ArgumentNullException.ThrowIfNull(measured);

        return Fill(
            measured.Text,
            availableWidth,
            firstLineWidth,
            language,
            measured.WidthBetween,
            tabs,
            widthOfLine,
            cellBroken,
            tabs?.Hyphenation is null ? null : measured.HyphenWidthAt);
    }

    /// <summary>
    /// Breaks a paragraph into lines that fit a width.
    /// </summary>
    /// <param name="text">The paragraph's text, without its terminating mark.</param>
    /// <param name="emSize">The em size the text is set at.</param>
    /// <param name="availableWidth">The width a line has to fit in.</param>
    /// <param name="firstLineWidth">
    /// The width available to the first line, when it differs — a first-line indent narrows it and a
    /// hanging indent widens it. Defaults to <paramref name="availableWidth"/>.
    /// </param>
    /// <param name="language">A BCP 47 tag, for the language-specific break rules.</param>
    /// <param name="options">
    /// How to shape. The default is what Writer does, so passing nothing gives Writer's line breaks.
    /// </param>
    /// <param name="tabs">
    /// The paragraph's format, or null. Two things are read from it: its tab stops, consulted only for a
    /// line that holds a tab, and whether a justified line may squeeze its blanks
    /// (<see cref="ParagraphFormat.ShrinksJustifiedBlanks"/>). A paragraph that is neither tabbed nor
    /// justified measures exactly as it would without this parameter.
    /// </param>
    /// <param name="widthOfLine">
    /// The width one line may use, asked once per line just before it is filled. Null for the ordinary
    /// case, where every line but the first gets the same width; it exists for text flowing round a
    /// floating frame, where the width depends on how far down the paragraph the line sits.
    /// </param>
    public List<TextLine> Fill(
        string text,
        Length emSize,
        Length availableWidth,
        Length? firstLineWidth = null,
        string? language = null,
        ShapingOptions? options = null,
        ParagraphFormat? tabs = null,
        Func<int, IReadOnlyList<TextLine>, Length>? widthOfLine = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        ShapingOptions shaping = options ?? new ShapingOptions(Language: language);
        ShapedText shaped = text.Length == 0
            ? ShapedText.Empty
            : _measurer.Shape(text, shaping);

        Length hyphen = tabs?.Hyphenation is null
            ? Length.Zero
            : _measurer.Measure("-", emSize, shaping);

        return Fill(
            text,
            availableWidth,
            firstLineWidth,
            language,
            (from, to) => shaped.WidthBetween(from, to, emSize),
            tabs,
            widthOfLine,
            hyphenWidthAt: tabs?.Hyphenation is null ? null : _ => hyphen);
    }

    /// <summary>
    /// The fill loop itself, over any way of measuring a range.
    /// </summary>
    /// <remarks>
    /// Taking the measurement as a function is what keeps one algorithm serving both the single-face and
    /// the mixed-run cases. The alternative — two loops — would break lines differently the first time
    /// either was touched, and the whole point of this code is that it breaks them where Writer does.
    /// </remarks>
    private List<TextLine> Fill(
        string text,
        Length availableWidth,
        Length? firstLineWidth,
        string? language,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs = null,
        Func<int, IReadOnlyList<TextLine>, Length>? widthOfLine = null,
        IReadOnlyList<CellBrokenSpan>? cellBroken = null,
        Func<int, Length>? hyphenWidthAt = null)
    {
        List<TextLine> lines = [];
        if (text.Length == 0)
        {
            // Not zero. A paragraph with no text at all can still carry an as-character object — a
            // logo on a line of its own is written that way by every reader but the OOXML one — and
            // that line's width is what its alignment is measured against.
            lines.Add(new TextLine(0, 0, 0, widthBetween(0, 0), EndsParagraph: true));
            return lines;
        }

        IReadOnlyList<int> opportunities = _breaker.FindBreakOpportunities(text, language);
        HashSet<int> mandatory = [.. _breaker.FindMandatoryBreaks(text, language)];

        // A field's own stretches break at every cell, which is a break the iterator does not
        // offer and cannot be asked for: it is added here rather than in the breaker because it
        // belongs to the *content* — one range of one paragraph — and not to the language's rules.
        if (cellBroken is { Count: > 0 })
        {
            opportunities = CellBreaks.Merge(opportunities, cellBroken, text);
        }

        // A justified paragraph in a file that asks for Word 2013's justification may overrun its room
        // by whatever squeezing its blanks recovers. Resolved once per paragraph rather than per
        // candidate line, so a paragraph that is not justified measures exactly as it did before this
        // existed.
        bool shrinks = tabs is { ShrinksJustifiedBlanks: true }
                       && tabs.Alignment is TextAlignment.Justify or TextAlignment.Distribute;

        int lineStart = 0;
        int nextOpportunity = 0;

        // Resolved once: a paragraph whose language has no pattern file is not hyphenated at all, which is
        // LibreOffice's answer too — `getMatchingDict` finds nothing and `hyphenate` returns null.
        HyphenationRules? hyphenation = tabs?.Hyphenation;
        IHyphenator? hyphenator = hyphenation is not null && hyphenWidthAt is not null
            ? Hyphenators.For(language)
            : null;
        int hyphenatedRun = 0;

        while (lineStart < text.Length)
        {
            OfficeOperation.Poll();
            Length limit = widthOfLine is not null
                ? widthOfLine(lines.Count, lines)
                : lines.Count == 0 ? firstLineWidth ?? availableWidth : availableWidth;

            int chosen = -1;
            Length chosenWidth = Length.Zero;
            int chosenVisibleEnd = lineStart;
            Length chosenAllowance = Length.Zero;
            Length chosenBlanks = Length.Zero;

            // Where a tab ends the line whatever the break iterator offered, or the text's end when
            // none does. Writer's portions are built in document order and a tab that reaches the
            // line's boundary ends it in front of itself — see <see cref="TabRuler.BreakAt"/>. Only
            // for a paragraph that clamps its stops, which is what says the text is a Writer frame's.
            int tabEnd = TabEnd(text, lineStart, widthBetween, tabs, lines.Count == 0, limit);

            // Walk the opportunities after the line's start, keeping the last that fits.
            int probe = nextOpportunity;
            while (probe < opportunities.Count)
            {
                int end = opportunities[probe];
                if (end <= lineStart)
                {
                    probe++;
                    continue;
                }

                // Nothing past the tab is on this line, so nothing past it is a candidate for its end.
                if (end > tabEnd) break;

                int visibleEnd = TrimTrailingSpaces(text, lineStart, end);
                Length width = Measure(
                    text, lineStart, visibleEnd, widthBetween, tabs, lines.Count == 0, limit);

                // The allowance belongs to the candidate rather than to the line already chosen: the
                // word being tried brings a blank with it, and that blank can be squeezed too.
                Length blanks = shrinks
                    ? JustificationShrink.BlanksOn(text, lineStart, visibleEnd, widthBetween)
                    : Length.Zero;
                Length allowance = shrinks
                    ? JustificationShrink.AllowanceFor(text, lineStart, visibleEnd, widthBetween)
                    : Length.Zero;

                if (width > limit + allowance && chosen >= 0) break;

                // Reaching the floor is what lets this word onto the line; it is not what decides it.
                // Word 2013's justification weighs the blanks this candidate would squeeze against the
                // blanks the candidate already chosen would stretch, and keeps the shorter line when
                // stretching wins — see <see cref="JustificationShrink.PrefersShrinking"/>.
                if (shrinks && chosen >= 0 && width > limit
                    && !JustificationShrink.PrefersShrinking(
                        limit, chosenWidth, chosenBlanks, width, blanks))
                {
                    break;
                }

                chosen = end;
                chosenWidth = width;
                chosenVisibleEnd = visibleEnd;
                chosenAllowance = allowance;
                chosenBlanks = blanks;
                probe++;

                // A required break ends the line whether or not the text would have fitted: a manual
                // line break is not a suggestion, and running past one would put two of the document's
                // lines on one of the page's.
                if (mandatory.Contains(end)) break;

                // The first opportunity is taken whatever it measures, and chopped below if it does
                // not fit: an empty line followed by the same problem is not an option.
                if (width > limit + allowance) break;
            }

            // The tab's own cut, which is not a break opportunity and so was never a candidate above.
            // It wins whenever the text in front of it fits, because that is where Writer's line
            // ended; when it does not fit the line breaks earlier and the tab stays on the next one.
            if (tabEnd < text.Length && chosen < tabEnd)
            {
                int visibleEnd = TrimTrailingSpaces(text, lineStart, tabEnd);
                Length width = Measure(
                    text, lineStart, visibleEnd, widthBetween, tabs, lines.Count == 0, limit);
                Length allowance = shrinks
                    ? JustificationShrink.AllowanceFor(text, lineStart, visibleEnd, widthBetween)
                    : Length.Zero;

                if (chosen <= lineStart || width <= limit + allowance)
                {
                    chosen = tabEnd;
                    chosenVisibleEnd = visibleEnd;
                    chosenWidth = width;
                    chosenAllowance = allowance;
                }
            }

            if (chosen <= lineStart)
            {
                // No opportunity at all past this point, which happens when the text ends without one.
                chosen = text.Length;
                chosenVisibleEnd = TrimTrailingSpaces(text, lineStart, chosen);
                chosenWidth = Measure(
                    text, lineStart, chosenVisibleEnd, widthBetween, tabs, lines.Count == 0, limit);
                chosenAllowance = shrinks
                    ? JustificationShrink.AllowanceFor(
                        text, lineStart, chosenVisibleEnd, widthBetween)
                    : Length.Zero;
            }

            // A break directly after a solidus is not taken when a blank stands close behind it:
            // the whole path is pulled onto the next line instead. See <see cref="GluedAcrossSolidus"/>.
            int chopEnd = chosen;
            bool forceChop = false;

            if (chosen > lineStart && chosen < text.Length && text[chosen - 1] == '/'
                && !CellBreaks.BreaksInside(cellBroken, chosen))
            {
                int glued = GluedAcrossSolidus(text, chosen);

                if (glued > lineStart)
                {
                    chosen = glued;
                    chosenVisibleEnd = TrimTrailingSpaces(text, lineStart, chosen);
                    chosenWidth = Measure(
                        text, lineStart, chosenVisibleEnd, widthBetween, tabs, lines.Count == 0,
                        limit);
                    chosenAllowance = shrinks
                        ? JustificationShrink.AllowanceFor(
                            text, lineStart, chosenVisibleEnd, widthBetween)
                        : Length.Zero;
                    chopEnd = chosen;
                    probe = nextOpportunity;
                }
                else if (glued < chosen)
                {
                    // The blank is the line's own start, so gluing would leave an empty line.
                    // EditEngine gives up on the break iterator and cuts at the fitting limit —
                    // "No separator in line => Chop!" — which packs the path across the line
                    // rather than starting a new one for it.
                    forceChop = true;
                    chopEnd = text.Length;
                }
            }

            // The line's trailing blanks are wider than the line. A word processor lets them hang;
            // EditEngine breaks behind the blank that overflowed and compresses it, which is what
            // turns a cell holding a hundred spaces into four lines rather than one. Only the blanks
            // are affected: when the character that overflows is not one, this leaves the chosen
            // break alone and the chop below deals with it as it always has.
            if (_breaksOverflowingBlanks && !forceChop && chosen > lineStart
                && Measure(text, lineStart, chosen, widthBetween, tabs, lines.Count == 0, limit)
                       > limit + chosenAllowance
                && OverflowingBlank(
                       text, lineStart, chosen, limit, widthBetween, tabs, lines.Count == 0)
                       is { } behind)
            {
                chosen = behind;
                chosenVisibleEnd = TrimTrailingSpaces(text, lineStart, chosen);
                chosenWidth = Measure(
                    text, lineStart, chosenVisibleEnd, widthBetween, tabs, lines.Count == 0, limit);
                chosenAllowance = shrinks
                    ? JustificationShrink.AllowanceFor(
                        text, lineStart, chosenVisibleEnd, widthBetween)
                    : Length.Zero;
                chopEnd = chosen;
                probe = nextOpportunity;
            }

            // The word that runs past the margin may be hyphenated instead of carried whole to the next
            // line. See <see cref="Hyphenate"/>.
            bool hyphenated = false;
            if (hyphenator is not null && !forceChop
                && (hyphenation!.MaxConsecutive <= 0 || hyphenatedRun < hyphenation.MaxConsecutive)
                && hyphenation.NoHyphenationAt?.Contains(lineStart) != true
                && !(tabEnd < text.Length && chosen == tabEnd)
                && !mandatory.Contains(chosen)
                && Hyphenate(
                       text, lineStart, chosen, chosenVisibleEnd, limit, language, hyphenation, hyphenator,
                       opportunities, (from, to) => Measure(text, from, to, widthBetween, tabs, lines.Count == 0, limit),
                       hyphenWidthAt!) is { } broken)
            {
                chosen = broken.End;
                chosenVisibleEnd = broken.End;
                chosenWidth = broken.Width;
                chosenAllowance = Length.Zero;
                probe = nextOpportunity;
                hyphenated = true;
            }

            // Nothing between this line's start and its first break opportunity fits. The word is
            // chopped rather than left hanging over the margin — see the remarks on this class.
            if (!hyphenated && (forceChop || chosenWidth > limit + chosenAllowance)
                && Chop(text, lineStart, chopEnd, limit, widthBetween, tabs, lines.Count == 0)
                       is { } cut
                && cut > lineStart)
            {
                chosen = cut;
                chosenVisibleEnd = cut;
                chosenWidth = Measure(text, lineStart, cut, widthBetween, tabs, lines.Count == 0, limit);

                // A chop lands inside a word, so the line it leaves holds no blank between two words
                // and there is nothing left to squeeze.
                chosenAllowance = Length.Zero;
                probe = nextOpportunity;
            }

            // Everything above fitted the line, so it left out the blank a trailing right, centred or
            // decimal stop advances across — Writer settles that blank in PostFormat, once the text
            // after the tab is already on the line. The line's own width is the drawn one, and it is
            // what the drawing layer resolves the same stops against, so it is taken once here rather
            // than at each of the candidates the loop above threw away.
            if (tabs is { ClampsTabsAtLineEdge: true })
            {
                chosenWidth = Measure(
                    text, lineStart, chosenVisibleEnd, widthBetween, tabs, lines.Count == 0, limit,
                    placed: true);
                if (hyphenated) chosenWidth += hyphenWidthAt!(chosenVisibleEnd - 1);
            }

            lines.Add(new TextLine(
                lineStart, chosen, chosenVisibleEnd, chosenWidth,
                EndsParagraph: chosen >= text.Length,
                ShrinkAllowance: chosenAllowance,
                Hyphenated: hyphenated));

            hyphenatedRun = hyphenated ? hyphenatedRun + 1 : 0;
            lineStart = chosen;
            nextOpportunity = probe;
        }

        // A break on the very last character opens a line the loop above never enters, because it
        // leaves lineStart == text.Length. That line is empty and it is still a line: LibreOffice
        // makes a paragraph ending in one exactly as tall as the same paragraph followed by an
        // empty one. Measured on slide-trailing-break.pptx, four boxes differing only in where the
        // break sits — a trailing a:br and an explicit empty paragraph both put the next
        // paragraph's baseline 48.02 pt down, against 24.01 for no break at all.
        //
        // Only a *line* separator, never a paragraph one. The readers do not agree on which
        // character to use for a manual break, and two of the characters EndsLine accepts —
        // '\r' and '\n' — are also what a reader may leave on the end of a paragraph's text to
        // mean the paragraph ends there. Adding a line for those would lengthen every paragraph in
        // the corpus, so the set here is the strict subset that can only be a break inside one.
        if (lines.Count > 0 && IsLineSeparator(text[^1]))
        {
            lines.Add(new TextLine(
                text.Length, text.Length, text.Length, Length.Zero, EndsParagraph: true));
        }

        SpillTrailingNoBreakSpace(
            text, lines, opportunities, widthBetween, tabs, availableWidth, firstLineWidth,
            widthOfLine);

        return lines;
    }

    /// <summary>
    /// Moves a trailing no-break space and its blanks onto lines of their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See <see cref="ParagraphFormat.SpillsTrailingNoBreakSpace"/> for what was measured and where.
    /// The whole rule is here: find the no-break space that the paragraph's trailing blanks hang off,
    /// break at the last opportunity that still precedes it — everything after that is bound to it and
    /// travels with it, which is what drags the preceding word down — and then, unless the paragraph is
    /// block-adjusted, add the empty line that follows.
    /// </para>
    /// <para>
    /// A post-pass rather than a case inside the fill loop because it is not a fitting decision: the
    /// tail fits the line it was put on, by a wide margin, on every document this fires for. Expressing
    /// it as a width would mean inventing one.
    /// </para>
    /// <para>
    /// The two rows that pin the shape down both fall out of this and neither was written into it. A
    /// paragraph ending <c>"lanes." NBSP SP</c> has its last opportunity before <c>lanes</c>, so the
    /// new line carries a visible word and the paragraph gains one text line and one blank one. A
    /// paragraph ending <c>"lanes." SP NBSP SP</c> has one immediately before the no-break space, so
    /// the new line is invisible, the text-line count does not move, and both extra pitches show up as
    /// the gap to whatever follows.
    /// </para>
    /// </remarks>
    private static void SpillTrailingNoBreakSpace(
        string text,
        List<TextLine> lines,
        IReadOnlyList<int> opportunities,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs,
        Length availableWidth,
        Length? firstLineWidth,
        Func<int, IReadOnlyList<TextLine>, Length>? widthOfLine)
    {
        if (tabs is not { SpillsTrailingNoBreakSpace: true } || lines.Count == 0) return;

        // The tail has to be a no-break space and then ordinary blanks, and nothing after them.
        int blanks = text.Length;
        while (blanks > 0 && text[blanks - 1] == ' ') blanks--;

        if (blanks == text.Length || blanks == 0 || text[blanks - 1] != '\u00A0') return;

        int hard = blanks - 1;
        TextLine last = lines[^1];

        Length LimitAt(int index) => widthOfLine is not null
            ? widthOfLine(index, lines)
            : index == 0 ? firstLineWidth ?? availableWidth : availableWidth;

        TextLine LineOf(int start, int end, int index)
        {
            int visible = TrimTrailingSpaces(text, start, end);
            Length limit = LimitAt(index);
            return new TextLine(
                start, end, visible,
                Measure(text, start, visible, widthBetween, tabs, index == 0, limit),
                EndsParagraph: end >= text.Length);
        }

        // The last opportunity that still precedes the no-break space, and it has to leave something
        // on the line it breaks: a split at the line's own start would move the whole line and make no
        // progress, so such a paragraph keeps the arrangement the fill loop gave it.
        int split = 0;
        foreach (int at in opportunities)
        {
            if (at > last.Start && at <= hard && at > split) split = at;
        }

        if (split > last.Start)
        {
            lines[^1] = LineOf(last.Start, split, lines.Count - 1);
            lines.Add(LineOf(split, text.Length, lines.Count));
        }

        // Block adjustment takes the trailing blanks into the justification instead of spilling them,
        // and is the one arrangement that does not gain the second line.
        if (tabs.Alignment is TextAlignment.Justify or TextAlignment.Distribute) return;

        lines.Add(new TextLine(
            text.Length, text.Length, text.Length, Length.Zero, EndsParagraph: true));
    }

    /// <summary>
    /// True for a character that can only be a manual line break, never the end of a paragraph.
    /// </summary>
    /// <remarks>
    /// A strict subset of <see cref="EndsLine"/>. OOXML's <c>a:br</c> and <c>w:br</c> and
    /// ODF's <c>text:line-break</c> all arrive as U+2028, and a binary PowerPoint's is U+000B; the
    /// three the subset drops — <c>'\r'</c>, <c>'\n'</c> and U+2029 — are the ones a reader might
    /// be using to mark the end of the paragraph itself.
    /// </remarks>
    private static bool IsLineSeparator(char character)
        => character is '\u2028' or '\u000B' or '\u000C' or '\u0085';

    /// <summary>
    /// How far back a path may be pulled when the chosen break sits directly after a solidus.
    /// </summary>
    /// <remarks><see cref="GluedAcrossSolidus"/>; <c>nOverlyLong</c>, and the comment beside it
    /// calls the figure arbitrary.</remarks>
    private const int SolidusGlueReach = 66;

    /// <summary>
    /// Where a break directly after <c>/</c> is moved back to, or the position itself when it stays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UAX #14 classes U+002F SOLIDUS as <c>SY</c>, symbols allowing a break after, and LibreOffice
    /// then takes that back for paths and URIs — <c>BreakIterator_Unicode::getLineBreak</c>,
    /// <c>i18npool/source/breakiterator/breakiterator_unicode.cxx:541-560</c>, i#17155. Having
    /// picked a break, it asks whether the character before it is a solidus, and if so looks
    /// <em>backwards</em> for whitespace and moves the break to just after it — pulling the whole
    /// path onto the next line rather than splitting it at a separator. The search gives up after
    /// <see cref="SolidusGlueReach"/> characters, so a long path is still split at a solidus.
    /// </para>
    /// <para>
    /// This is not a break-opportunity rule and cannot be written as one, which is why it lives
    /// here rather than in <see cref="LineBreaker"/>: it jumps <em>past</em> any other opportunity
    /// standing between the blank and the solidus, so removing the solidus from the opportunity set
    /// would break at the intervening hyphen instead.
    /// </para>
    /// <para>
    /// Measured on twelve probe cells at one column width, read out of LibreOffice's own PDF.
    /// <c>CAT.IDE.A.170/CAT.IDE.H.170</c> alone in a cell breaks after its solidus, because nothing
    /// stands behind it; the same string after <c>AMC1 </c> does not break there at all and is cut
    /// mid-number at the fitting limit, because the glue would land on the line's own start. Both
    /// halves are visible on one document — <c>tk-syllabus-comparison-document-v5.xlsx</c>, whose
    /// rows disagreed with LibreOffice's own <c>style:row-height</c> in both directions.
    /// </para>
    /// <para>
    /// One narrower case of the same rule is <strong>not</strong> reproduced: when the solidus break
    /// falls exactly on the last character that fits, LibreOffice refuses it outright and takes the
    /// previous opportunity (<c>breakiterator_unicode.cxx:494-496</c>). Deciding that needs the
    /// fitting limit measured to the character, where the two renderers agree only to within a
    /// rounding, so reproducing it would turn a rounding difference into a whole line.
    /// </para>
    /// </remarks>
    /// <param name="text">The paragraph's text.</param>
    /// <param name="chosen">The break position, whose preceding character is a solidus.</param>
    private static int GluedAcrossSolidus(string text, int chosen)
    {
        int floor = Math.Max(0, chosen - SolidusGlueReach);
        for (int index = chosen - 2; index >= floor; index--)
        {
            if (IsBreakingWhitespace(text[index])) return index + 1;
        }

        return chosen;
    }

    /// <summary>
    /// ICU's <c>u_isWhitespace</c>: a space that also separates lines, so never a no-break one.
    /// </summary>
    private static bool IsBreakingWhitespace(char character)
        => character is not ('\u00A0' or '\u2007' or '\u202F')
           && (char.IsWhiteSpace(character) || character is >= '\u001C' and <= '\u001F');

    /// <summary>
    /// Where the word that runs past the margin is hyphenated, and how wide the line then is; or null when
    /// it is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writer's rule, from <c>SwTextGuess::Guess</c> (<c>sw/source/core/text/guess.cxx</c>:302-420, 652-790)
    /// and the break iterator it asks (<c>BreakIterator_Unicode::getLineBreak</c>,
    /// <c>i18npool/source/breakiterator/breakiterator_unicode.cxx</c>:499-537). Only the word holding the
    /// <em>cut position</em> — the first character that does not fit — is ever hyphenated, and nothing is
    /// when that character is a blank or starts a word. The hyphenator is told how much of the word may
    /// stay on the line: everything before the first character that no longer fits once a hyphen is
    /// appended (<c>nHyphPos</c>). It answers with the last pattern point that leaves at most that much, at
    /// least the paragraph's leading minimum before it and its trailing minimum after
    /// (<c>lingucomponent/source/hyphenator/hyphen/hyphenimp.cxx</c>:437-448), and only for a word at least
    /// the minimum length.
    /// </para>
    /// <para>
    /// The hyphenation zone (<c>guess.cxx</c>:323-400): when a blank lies between the cut position and the
    /// first character that does not fit in the line less the zone, the line ends at that blank rather than
    /// being hyphenated — a short gap at the end of a line is better than a hyphen.
    /// </para>
    /// </remarks>
    private static (int End, Length Width)? Hyphenate(
        string text,
        int lineStart,
        int chosen,
        int chosenVisibleEnd,
        Length limit,
        string? language,
        HyphenationRules rules,
        IHyphenator hyphenator,
        IReadOnlyList<int> opportunities,
        Func<int, int, Length> measure,
        Func<int, Length> hyphenWidthAt)
    {
        // The cut position, as `GetTextBreak` finds it. A chosen line that already overflows is a word
        // alone on its line, and the cut is inside it.
        int cut = chosen > lineStart && measure(lineStart, chosenVisibleEnd) <= limit
            ? chosenVisibleEnd
            : lineStart;
        while (cut < text.Length && measure(lineStart, cut + 1) <= limit) cut++;

        if (cut >= text.Length || cut <= lineStart || IsHyphenationBlank(text[cut])) return null;

        // A cut at a break opportunity is a word boundary, and the line ends there unhyphenated.
        if (Contains(opportunities, cut)) return null;

        // Back over punctuation the cut landed on, to the word it closes.
        int at = cut;
        while (at > lineStart && !IsWordCharacter(text, at) && char.IsPunctuation(text[at])) at--;
        if (!IsWordCharacter(text, at)) return null;

        int wordStart = at;
        while (wordStart > 0 && IsWordCharacter(text, wordStart - 1)) wordStart--;
        int wordEnd = at + 1;
        while (wordEnd < text.Length && IsWordCharacter(text, wordEnd)) wordEnd++;

        string word = text[wordStart..wordEnd];
        if (word.Length < rules.MinWordLength) return null;
        if (rules.NoCaps && word.Any(char.IsLetter) && !word.Any(char.IsLower)) return null;

        if (rules.Zone > Length.Zero && InZone(text, lineStart, cut, limit - rules.Zone, measure)) return null;

        // How much of the word fits with a hyphen after it.
        Length hyphen = hyphenWidthAt(cut);
        int fitting = lineStart;
        while (fitting < text.Length && measure(lineStart, fitting + 1) + hyphen <= limit) fitting++;
        int maxLeading = fitting - wordStart;
        if (maxLeading <= 0) return null;

        int lead = Math.Max(rules.MinLeading, 2);
        int best = -1;
        foreach (int point in hyphenator.FindHyphenationPoints(word, language ?? Hyphenators.DefaultLanguage))
        {
            if (point < lead || word.Length - point < rules.MinTrailing || point > maxLeading) continue;
            if (wordStart + point <= lineStart) continue;
            best = Math.Max(best, point);
        }

        if (best < 0) return null;

        int end = wordStart + best;
        return (end, measure(lineStart, end) + hyphenWidthAt(end - 1));
    }

    /// <summary>
    /// True when a blank lies between the cut position and where text stops fitting in the line less its
    /// hyphenation zone, which is what makes the line end at that blank instead.
    /// </summary>
    private static bool InZone(
        string text, int lineStart, int cut, Length zoneLimit, Func<int, int, Length> measure)
    {
        int zone = lineStart;
        if (zoneLimit > Length.Zero)
        {
            while (zone < text.Length && measure(lineStart, zone + 1) <= zoneLimit) zone++;
        }

        for (int at = cut; at >= lineStart && at >= zone; at--)
        {
            if (IsHyphenationBlank(text[at])) return true;
        }

        return false;
    }

    /// <summary>The three blanks Writer's guess treats as one: space, ideographic space, six-per-em.</summary>
    private static bool IsHyphenationBlank(char c) => c is ' ' or '\u3000' or '\u2006';

    /// <summary>
    /// True for a character inside a dictionary word: a letter, a digit or a mark, or an apostrophe with a
    /// letter on each side — ICU's dictionary-word boundary keeps <c>don't</c> whole and stops at a hyphen.
    /// </summary>
    private static bool IsWordCharacter(string text, int at)
    {
        if (at < 0 || at >= text.Length) return false;

        char c = text[at];
        if (char.IsLetterOrDigit(c)) return true;

        System.Globalization.UnicodeCategory category = char.GetUnicodeCategory(c);
        if (category is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark) return true;

        return c is '\'' or '\u2019'
               && at > 0 && at + 1 < text.Length
               && char.IsLetter(text[at - 1]) && char.IsLetter(text[at + 1]);
    }

    private static bool Contains(IReadOnlyList<int> sorted, int value)
    {
        int low = 0;
        int high = sorted.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) >> 1;
            if (sorted[middle] == value) return true;
            if (sorted[middle] < value) low = middle + 1;
            else high = middle - 1;
        }

        return false;
    }

    /// <summary>
    /// Where to cut a word that does not fit the line it starts, or null to leave it alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The last character position whose text still fits, and never the line's own start — a line
    /// of no characters would make no progress and the fill loop would not terminate. LibreOffice
    /// arrives at the same position from the other end: <c>ImpEditEngine::ImpBreakLine</c> walks
    /// the character-position array while it is under the remaining width
    /// (<c>editeng/source/editeng/impedit3.cxx:2016-2018</c>) and, when the break iterator offers
    /// nothing inside the line, takes it outright — "No separator in line =&gt; Chop!",
    /// <c>impedit3.cxx:2236-2247</c>, with the same guard against an empty line. Writer's
    /// <c>SwTextGuess::Guess</c> ends in the same place, cutting at <c>m_nCutPos</c> when no break
    /// position was found (<c>sw/source/core/text/guess.cxx:832-839</c>).
    /// </para>
    /// <para>
    /// It only applies to a word that is alone on its line: a line that has already fitted
    /// something breaks before the oversized word instead, and the word is chopped on the line it
    /// then starts. That is why the caller tests the chosen width rather than each candidate's.
    /// </para>
    /// <para>
    /// A cut never lands between a surrogate pair. It can still land inside a grapheme cluster
    /// whose base and marks are separate code points, which is what LibreOffice's own
    /// <c>iterateCodePoints</c> does too.
    /// </para>
    /// </remarks>
    private static int? Chop(
        string text,
        int lineStart,
        int end,
        Length limit,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs,
        bool isFirstLine)
    {
        int first = Whole(text, lineStart + 1);
        if (first >= end) return null;

        // Binary search: prefix widths only grow, so the last position that fits is found in a
        // logarithm of the word's length rather than by measuring every prefix of it.
        int low = first;
        int high = end - 1;
        int best = first;

        while (low <= high)
        {
            int middle = Whole(text, low + ((high - low) / 2));
            if (middle <= first) { low = first + 1; continue; }
            if (middle >= end) { high = middle - 2; continue; }

            if (Measure(text, lineStart, middle, widthBetween, tabs, isFirstLine, limit) <= limit)
            {
                best = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
    }

    /// <summary>
    /// Where to break when a line's own blanks overflow it, or null when they do not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ImpEditEngine::ImpBreakLine</c> (<c>editeng/source/editeng/impedit3.cxx:2016-2035</c>) in
    /// two statements. It walks the character-position array — cumulative advances, every character
    /// counted — while it is still under the remaining width, giving the first character that does
    /// not fit; and if that character is a blank it breaks one past it, leaving the blank on the
    /// line to be compressed away.
    /// </para>
    /// <para>
    /// Null when the overflowing character is anything else, because that is a word too long for its
    /// line and the caller already has a rule for it: <see cref="Chop"/>, which is the same walk with
    /// EditEngine's other ending.
    /// </para>
    /// <para>
    /// Binary search rather than EditEngine's linear walk, for the same reason <see cref="Chop"/>
    /// uses one: prefix widths only grow, and a cell holding a thousand blanks would otherwise cost a
    /// measurement per blank per line.
    /// </para>
    /// </remarks>
    /// <returns>The position just past the overflowing blank, or null.</returns>
    private static int? OverflowingBlank(
        string text,
        int lineStart,
        int end,
        Length limit,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs,
        bool isFirstLine)
    {
        // The first character whose cumulative width reaches the limit, which is where EditEngine's
        // `while (… < nRemainingWidth) nBreakInLine++` leaves off.
        int low = lineStart;
        int high = end - 1;
        int at = end;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            if (Measure(text, lineStart, middle + 1, widthBetween, tabs, isFirstLine, limit) >= limit)
            {
                at = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        if (at >= end || text[at] != ' ') return null;

        // One past the blank, and never the line's own start: a line of no characters would make no
        // progress and the fill loop would not terminate.
        int behind = at + 1;
        return behind > lineStart && behind < end ? behind : null;
    }

    /// <summary>A cut position moved forward off the second half of a surrogate pair.</summary>
    private static int Whole(string text, int index)
    {
        while (index < text.Length && char.IsLowSurrogate(text[index])) index++;
        return index;
    }

    /// <summary>
    /// The width of a candidate line, with its tabs resolved when it has any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tab's width is where it lands rather than what the font says, so a line holding one cannot be
    /// measured as a difference of two prefix widths. The check for a tab comes first because nearly every
    /// line has none, and one that has none must measure exactly as it did before tabs existed.
    /// </para>
    /// <para>
    /// <c>placed</c> asks for the width the line will be <em>drawn</em> at rather than the width it is
    /// <em>fitted</em> by. The two differ by the blank a trailing right, centred or decimal stop advances
    /// across, which Writer settles only after the text following the tab is already on the line — see
    /// <see cref="TabRuler.WidthOf"/>. Fitting is what nearly every call here wants and so is the
    /// default; the filler asks for the placed width once, for the line it has settled on. The two
    /// coincide unless the paragraph clamps its stops, which is the only case where a stop can sit past
    /// the line's own edge at all.
    /// </para>
    /// </remarks>
    private static Length Measure(
        string text,
        int start,
        int end,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs,
        bool isFirstLine,
        Length? lineWidth = null,
        bool placed = false)
        => tabs is not null && TabRuler.HasTab(text, start, end)
            ? TabRuler.WidthOf(
                text, start, end, tabs, widthBetween, isFirstLine,
                RightEdge(tabs, isFirstLine, lineWidth),
                countsDeferredStretch: placed || !tabs.ClampsTabsAtLineEdge)
            : widthBetween(start, end);

    /// <summary>
    /// Where a tab ends the line whatever the break iterator offered, or the text's end when none does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TabRuler.BreakAt"/> is the rule and this is its guard: it is asked only of a paragraph
    /// that clamps its stops — which is what says the text belongs to a Writer text frame rather than to
    /// a slide's text body or a spreadsheet cell, both of which lay tabs out through other code — and
    /// only when the line actually holds one, since nearly none do and the walk costs a measurement per
    /// tab.
    /// </para>
    /// <para>
    /// The line's boundary is <paramref name="lineWidth"/> converted into the coordinates the stops are
    /// stated in, which is the same conversion <see cref="RightEdge"/> makes without the end indent
    /// added back on: the break is decided against <c>rInf.Width()</c> and a stop is clamped against the
    /// frame's edge, and those two differ by exactly that indent.
    /// </para>
    /// </remarks>
    private static int TabEnd(
        string text,
        int lineStart,
        Func<int, int, Length> widthBetween,
        ParagraphFormat? tabs,
        bool isFirstLine,
        Length? lineWidth)
        => tabs is { ClampsTabsAtLineEdge: true } && lineWidth is { } width
           && TabRuler.HasTab(text, lineStart, text.Length)
            ? TabRuler.BreakAt(
                  text, lineStart, tabs, widthBetween, isFirstLine,
                  tabs.TabLineOffset(isFirstLine) + width,
                  RightEdge(tabs, isFirstLine, width))
              ?? text.Length
            : text.Length;

    /// <summary>
    /// The text frame's right boundary in the coordinates the tab stops are stated in, or null when the
    /// caller did not say how wide the line is.
    /// </summary>
    /// <remarks>
    /// The stops are measured from the paragraph's tab origin and the width from the line's own start, so
    /// the two agree only after the line's offset is added back. See
    /// <see cref="ParagraphFormat.TabLineOffset"/>, which is negative inside a hanging indent — where the
    /// boundary is correspondingly nearer. The paragraph's <em>end</em> indent is then added back on,
    /// because the boundary Writer clamps a stop at is the frame's and not the line's: the line's width
    /// already has the indent taken out of it and the frame's edge is that much further right.
    /// </remarks>
    private static Length? RightEdge(ParagraphFormat tabs, bool isFirstLine, Length? lineWidth)
        => tabs.ClampsTabsAtLineEdge && lineWidth is { } width
            ? tabs.TabLineOffset(isFirstLine) + width + tabs.EndIndent
            : null;

    /// <summary>
    /// Where a line's visible text ends: past its trailing spaces and its terminating break.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the space characters that can hang past a margin. A tab is not one of them — it advances to
    /// a stop and so has a width the line has to hold — and neither is a non-breaking space, whose
    /// whole purpose is to be part of the text around it.
    /// </para>
    /// <para>
    /// A mandatory break ends the line and takes no room on it, which is Writer's break portion: zero
    /// width, and no glyph. Leaving it in would put a <c>.notdef</c> box at the end of every line that
    /// ends in a manual break — and, worse, would count its advance towards the line's width, so a
    /// justified line would stretch by one glyph too little.
    /// </para>
    /// </remarks>
    private static int TrimTrailingSpaces(string text, int start, int end)
    {
        int at = Math.Min(end, text.Length);
        while (at > start && (text[at - 1] == ' ' || EndsLine(text[at - 1]))) at--;
        return at;
    }

    /// <summary>
    /// True for a character UAX #14 gives a mandatory break: one that ends a line and takes no room on it.
    /// </summary>
    /// <remarks>
    /// All of them, not just the line separator, because the readers do not agree on which to use — ODF's
    /// and OOXML's manual breaks arrive as U+2028 and RTF's as a newline, and both mean the same thing.
    /// </remarks>
    private static bool EndsLine(char character)
        => character is '\u2028' or '\u2029' or '\n' or '\r' or '\u000B' or '\u000C' or '\u0085';
}
