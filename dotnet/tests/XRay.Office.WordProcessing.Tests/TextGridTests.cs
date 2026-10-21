using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A page's text grid, DOCX's <c>w:docGrid</c>: which paragraphs snap to it, and how a snapped line is
/// sized.
/// </summary>
/// <remarks>
/// <para>
/// Every number here was measured on 26.8.1.1 with an authored DOCX of fourteen paragraphs and a table in
/// five variants — no grid, <c>lines</c> at 360 and 200 twips, <c>linesAndChars</c> at 312, and
/// <c>lines</c> with <c>w:adjustLineHeightInTable</c>. This tree draws every line of all five within
/// 0.05 pt of the reference, where it drew them up to 79.95 pt away before.
/// <c>probes/words-textgrid-r268/</c>.
/// </para>
/// <para>
/// The rules are <c>SwTextFormatter::CalcRealHeight</c>'s grid branch
/// (<c>sw/source/core/text/itrform2.cxx</c>:2275-2345), <c>SwTextFrame::FormatEmpty</c>'s grid height
/// (<c>porrst.cxx</c>:489-494) and <c>SwTextFrame::GetLineSpace</c>, which knows nothing of the grid.
/// </para>
/// </remarks>
public sealed class TextGridTests
{
    private const string Lines360 = """<w:docGrid w:type="lines" w:linePitch="360"/>""";

    private const string Words =
        "Alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima mike november oscar papa "
        + "quebec romeo sierra tango uniform victor whiskey xray yankee zulu alpha bravo charlie delta echo";

    private const string Serif =
        """<w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/><w:sz w:val="22"/>""";

    /// <summary>Which grid types snap lines, and what an absent or default type means.</summary>
    [Theory]
    [InlineData("""<w:docGrid w:type="lines" w:linePitch="360"/>""", 360L)]
    [InlineData("""<w:docGrid w:type="linesAndChars" w:linePitch="312"/>""", 312L)]
    [InlineData("""<w:docGrid w:type="snapToChars" w:linePitch="400"/>""", 400L)]
    [InlineData("""<w:docGrid w:type="default" w:linePitch="360"/>""", null)]
    [InlineData("""<w:docGrid w:linePitch="360"/>""", null)]
    public void TheGridTypeDecidesWhetherLinesSnap(string grid, long? pitch)
    {
        TextGridSnap? snap = Body(Paragraph("A"), grid)[0].Format.TextGrid;

        if (pitch is null)
        {
            snap.ShouldBeNull();
            return;
        }

        snap.ShouldNotBeNull().Pitch.ShouldBe(Length.FromTwips(pitch.Value));
        snap.Value.Snaps.ShouldBeTrue();
    }

    /// <summary><c>w:snapToGrid w:val="0"</c>, stated directly or on the style, keeps a paragraph off it.</summary>
    [Fact]
    public void AParagraphCanOptOut()
    {
        const string Styles = """
            <w:style w:type="paragraph" w:styleId="Free"><w:name w:val="Free"/>
              <w:pPr><w:snapToGrid w:val="0"/></w:pPr></w:style>
            """;

        List<PageParagraph> paragraphs = Body(
            Paragraph("A", pPr: """<w:snapToGrid w:val="0"/>""")
            + Paragraph("B", pPr: """<w:pStyle w:val="Free"/>""")
            + Paragraph("C"),
            Lines360,
            Styles);

        paragraphs[0].Format.TextGrid.ShouldBeNull();
        paragraphs[1].Format.TextGrid.ShouldBeNull();
        paragraphs[2].Format.TextGrid.ShouldNotBeNull();
    }

    /// <summary>
    /// A cell's paragraph is on the grid for the East Asian height but takes no line spacing — unless the
    /// document states <c>w:adjustLineHeightInTable</c>.
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("<w:compat><w:adjustLineHeightInTable/></w:compat>", true)]
    public void ATableCellSnapsOnlyWhenTheDocumentAsks(string settings, bool snaps)
    {
        string table = $"""
            <w:tbl><w:tblPr><w:tblW w:w="4000" w:type="dxa"/></w:tblPr><w:tblGrid><w:gridCol w:w="4000"/></w:tblGrid>
              <w:tr><w:tc>{Paragraph("Cell")}</w:tc></w:tr></w:tbl>
            {Paragraph("After")}
            """;

        WordProcessingPages pages = MiniDocx.Layout(table, settings: settings, sectionProperties: Section(Lines360));
        PageParagraph cell = pages.Pages[0].Tables.SelectMany(placed => placed.Cells)
            .SelectMany(placed => placed.Content!.Blocks).OfType<PageParagraph>().First();

        cell.Format.TextGrid.ShouldNotBeNull().Snaps.ShouldBe(snaps);
    }

    /// <summary>
    /// An 11 pt single-spaced line is one 18 pt pitch with its text centred in it; a 150 % paragraph's
    /// later lines are one and a half pitches; an at-least of nought takes the line off the grid.
    /// </summary>
    [Fact]
    public void LinesAreWholePitchesWithTheirTextCentred()
    {
        string body = Paragraph(Words)
            + Paragraph(Words, spacing: """w:line="360" w:lineRule="auto" """)
            + Paragraph(Words, spacing: """w:line="0" w:lineRule="atLeast" """);

        List<PlacedLine> grid = Lines(body, Lines360);
        List<PlacedLine> plain = Lines(body, grid: "");

        // 253 twips of 11 pt Liberation Serif centred in 360: (360 − 253) / 2 = 53 twips lower.
        (grid[0].Baseline - plain[0].Baseline).Twips.ShouldBe(53);
        Pitch(grid, 0).ShouldBe(Length.FromTwips(360));

        // The 150 % paragraph: its first line ignores the rule, its second is 1.5 × 360.
        PlacedLine[] second = [.. grid.Where(line => line.ParagraphIndex == 1)];
        (second[1].Baseline - second[0].Baseline).ShouldBe(Length.FromTwips(540));

        // The at-least of nought: the text's own 253 twips (tdf#164871), not a pitch.
        PlacedLine[] third = [.. grid.Where(line => line.ParagraphIndex == 2)];
        (third[1].Baseline - third[0].Baseline).ShouldBe(Length.FromTwips(253));
    }

    /// <summary>
    /// The 150 % paragraph hands half its <em>text</em> height to the next paragraph, not half a pitch:
    /// 26.8.1.1 sets the at-least-nought paragraph after it 6.30 pt lower than its predecessor's box.
    /// </summary>
    [Fact]
    public void TheLeadingHandedDownIsOfTheTextNotOfThePitch()
    {
        string body = Paragraph(Words, spacing: """w:line="360" w:lineRule="auto" """)
            + Paragraph(Words, spacing: """w:line="0" w:lineRule="atLeast" """);

        List<PlacedLine> lines = Lines(body, Lines360);
        PlacedLine last = lines.Last(line => line.ParagraphIndex == 0);
        PlacedLine next = lines.First(line => line.ParagraphIndex == 1);

        // The last line is 540 twips, the 180 of leading above a 360 pitch whose 253 twips of text sit
        // 53 below its top; the next box starts 126 twips (253 × 150 / 100 − 253) below it, and its
        // 253-twip line has no centring. So 360 − 53 + 126 = 433 twips, baseline to baseline, which is
        // the 21.65 pt the reference sets between the two paragraphs' text.
        (next.Baseline - last.Baseline).ShouldBe(Length.FromTwips(433));
    }

    /// <summary>
    /// An empty paragraph is exactly one pitch whatever its font, where a line of text that size takes
    /// two (<c>SwTextFrame::FormatEmpty</c>): 26.8.1.1 gives an empty 30 pt paragraph on an 18 pt grid 18 pt.
    /// </summary>
    [Fact]
    public void AnEmptyParagraphIsOnePitch()
    {
        const string Large =
            """<w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/><w:sz w:val="60"/>""";
        string body = Paragraph("Before")
            + $"""<w:p><w:pPr><w:spacing w:before="0" w:after="0"/><w:rPr>{Large}</w:rPr></w:pPr></w:p>"""
            + Paragraph("After");

        List<PlacedLine> lines = Lines(body, Lines360);

        (lines[2].Top - lines[0].Top).ShouldBe(Length.FromTwips(720));
    }

    /// <summary>
    /// A grid paragraph and an empty one do not take Word's 127 % East Asian line height (tdf#171275):
    /// 26.8.1.1 sets an empty 12 pt WenQuanYi Zen Hei paragraph 16.25 pt tall and a line of text 20.60.
    /// </summary>
    [Fact]
    public void AnEmptyOrGriddedParagraphTakesNoEastAsianHeight()
    {
        SystemFontResolver resolver = new(SystemFontIndex.Build());
        if (!resolver.Resolve(new FontRequest("WenQuanYi Zen Hei", 400, false)).FamilyName
                .Contains("WenQuanYi", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("WenQuanYi Zen Hei is not installed.");
        }

        const string Cjk =
            """<w:rFonts w:ascii="WenQuanYi Zen Hei" w:hAnsi="WenQuanYi Zen Hei" w:eastAsia="WenQuanYi Zen Hei"/><w:sz w:val="24"/>""";
        string Line(string text)
            => $"""<w:p><w:pPr><w:spacing w:before="0" w:after="0"/><w:rPr>{Cjk}</w:rPr></w:pPr>{(text.Length == 0 ? "" : $"<w:r><w:rPr>{Cjk}</w:rPr><w:t>{text}</w:t></w:r>")}</w:p>""";

        string body = Line("One") + Line("") + Line("Two") + Line("Three");

        List<PlacedLine> plain = Lines(body, grid: "");
        (plain[1].Top - plain[0].Top).ShouldBe(Length.FromTwips(412));
        (plain[2].Top - plain[1].Top).ShouldBe(Length.FromTwips(325));

        // On a 200-twip grid the text's own 325 twips take two pitches, not the 412 that would take three.
        List<PlacedLine> gridded = Lines(body, """<w:docGrid w:type="lines" w:linePitch="200"/>""");
        (gridded[3].Top - gridded[2].Top).ShouldBe(Length.FromTwips(400));
    }

    private static Length Pitch(List<PlacedLine> lines, int paragraph)
    {
        PlacedLine[] own = [.. lines.Where(line => line.ParagraphIndex == paragraph)];
        return own[1].Baseline - own[0].Baseline;
    }

    private static List<PlacedLine> Lines(string body, string grid)
        => [.. MiniDocx.Layout(body, Styles, sectionProperties: Section(grid)).Pages[0].Lines];

    private static List<PageParagraph> Body(string body, string grid, string styles = "")
        => [.. MiniDocx.Layout(body, Styles + styles, sectionProperties: Section(grid)).Blocks.OfType<PageParagraph>()];

    private const string Styles = $"""
        <w:docDefaults><w:rPrDefault><w:rPr>{Serif}</w:rPr></w:rPrDefault>
          <w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
        <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
        """;

    private static string Section(string grid) => $"""
        <w:pgSz w:w="12240" w:h="15840"/>
        <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
        {grid}
        """;

    private static string Paragraph(string text, string spacing = "", string pPr = "")
        => $"""<w:p><w:pPr>{pPr}<w:spacing w:before="0" w:after="0" {spacing}/><w:rPr>{Serif}</w:rPr></w:pPr><w:r><w:rPr>{Serif}</w:rPr><w:t>{text}</w:t></w:r></w:p>""";
}
