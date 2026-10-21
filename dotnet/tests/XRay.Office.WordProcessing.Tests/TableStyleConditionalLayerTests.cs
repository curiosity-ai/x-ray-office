using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// The halves of a table style's <c>w:tblStylePr</c> layers that writerfilter merges into the cell —
/// the borders and the paragraph properties — and a <c>w:tblInd</c> stated on the style.
/// </summary>
/// <remarks>
/// <para>
/// Measured against LibreOffice 26.8.1.1 on the four corpus documents that carry them:
/// <c>docx/sample_document.docx</c> page 2 goes from 2.02 % differing pixels (MAJOR, the header row's
/// 3 pt rule missing and the first column left-aligned) to 0.26 % (ok), <c>docx/word_tables.docx</c>
/// from 11.11 % (every header row 1 pt short) to 0.46 %, and <c>docx/extraction_test.docx</c> page 1
/// from 3.07 % to 1.22 %, its cell text moving from x = 77.4 to 72.0 against the reference's 72.1.
/// </para>
/// <para>
/// One residual is not this rule: <c>sample_document.docx</c>'s <c>firstCol</c> layer states
/// <c>w:rightChars="0"</c> beside <c>w:right="374"</c>, and 26.8.1.1 then draws the column's text with
/// no right indent at all. With the <c>*Chars</c> attributes removed from the file the reference draws
/// the 374 twips exactly as this tree does (Leaves ends at 207.75 against 207.60). The character-unit
/// indents are not modelled anywhere in the reader.
/// </para>
/// </remarks>
public sealed class TableStyleConditionalLayerTests
{
    private static readonly Length OnePoint = Length.FromPoints(1);

    private static readonly Length ThreePoints = Length.FromPoints(3);

    /// <summary>A <c>firstRow</c> layer's bottom border replaces the table's interior line under the heading.</summary>
    [Fact]
    public void TheHeadingRowTakesItsLayersBottomBorder()
    {
        PageTable table = Table(Style(firstRow: """
            <w:tcPr><w:tcBorders><w:top w:val="nil"/><w:bottom w:val="single" w:sz="24"/>
              <w:insideH w:val="nil"/></w:tcBorders></w:tcPr>
            """));

        table.Rows[0].Cells[0].Bottom().ShouldBe(ThreePoints, "the firstRow layer's 3 pt rule");
        table.Rows[0].Cells[1].Bottom().ShouldBe(ThreePoints);
        table.Rows[0].Cells[0].Top().ShouldBe(Length.Zero, "the layer clears the top with nil");
        table.Rows[1].Cells[0].Bottom().ShouldBe(OnePoint, "a body row keeps the table's interior line");
    }

    /// <summary>A layer the table's <c>w:tblLook</c> does not ask for draws nothing.</summary>
    [Fact]
    public void ALayerTheLookSwitchesOffIsNotApplied()
    {
        PageTable table = Table(
            Style(firstRow: """<w:tcPr><w:tcBorders><w:bottom w:val="single" w:sz="24"/></w:tcBorders></w:tcPr>"""),
            look: """<w:tblLook w:firstRow="0" w:firstColumn="0" w:noVBand="1" w:noHBand="1"/>""");

        table.Rows[0].Cells[0].Bottom().ShouldBe(OnePoint);
    }

    /// <summary>
    /// A cell clearing a visible style border with <c>nil</c> deletes both, so the table's own line shows.
    /// </summary>
    /// <remarks>
    /// <c>DomainMapperTableHandler.cxx</c>:857-892, "The style one would be visible, but then cleared away
    /// as direct formatting. Delete both, so that table formatting can become visible."
    /// </remarks>
    [Fact]
    public void ADirectNilOverAVisibleStyleBorderLetsTheTableShowThrough()
    {
        PageTable table = Table(
            Style(firstRow: """<w:tcPr><w:tcBorders><w:bottom w:val="single" w:sz="24"/></w:tcBorders></w:tcPr>"""),
            firstCellProperties: """<w:tcBorders><w:bottom w:val="nil"/></w:tcBorders>""");

        table.Rows[0].Cells[0].Bottom().ShouldBe(OnePoint, "both clearings deleted, the insideH stands");
        table.Rows[0].Cells[1].Bottom().ShouldBe(ThreePoints);
    }

    /// <summary>A <c>firstCol</c> layer's paragraph properties reach that column's paragraphs only.</summary>
    [Fact]
    public void TheFirstColumnTakesItsLayersParagraphProperties()
    {
        PageTable table = Table(Style(firstColumn: """<w:pPr><w:jc w:val="right"/><w:ind w:right="374"/></w:pPr>"""));

        PageParagraph leading = Paragraph(table.Rows[1].Cells[0]);
        leading.Format.Alignment.ShouldBe(TextAlignment.End);
        leading.Format.EndIndent.ShouldBe(Length.FromTwips(374));

        Paragraph(table.Rows[1].Cells[1]).Format.Alignment.ShouldBe(TextAlignment.Start);
    }

    /// <summary>A paragraph's own <c>w:jc</c> beats the conditional layer.</summary>
    [Fact]
    public void DirectParagraphFormattingBeatsTheLayer()
    {
        PageTable table = Table(
            Style(firstColumn: """<w:pPr><w:jc w:val="right"/></w:pPr>"""),
            firstParagraphProperties: """<w:jc w:val="center"/>""");

        Paragraph(table.Rows[0].Cells[0]).Format.Alignment.ShouldBe(TextAlignment.Centre);
    }

    /// <summary>
    /// A <c>w:tblInd</c> stated only on the style still counts as stated, so a Word 2007 file measures the
    /// indent to the cell's text and the table moves left by the cell's padding.
    /// </summary>
    [Fact]
    public void AnIndentStatedOnTheStyleIsMeasuredToTheText()
    {
        PageTable stated = Table(Style(tableProperties: """<w:tblInd w:w="0" w:type="dxa"/>"""));
        PageTable unstated = Table(Style());

        stated.LeftIndent.ShouldBe(Length.FromTwips(-108), "max(border/2, 108 twips of padding) to the left");
        unstated.LeftIndent.ShouldBe(Length.FromTwips(-10), "half the 1 pt border, Word's invented indent");
    }

    private static string Style(
        string? firstRow = null, string? firstColumn = null, string tableProperties = "")
        => $"""
            <w:style w:type="table" w:styleId="Probe">
              <w:name w:val="Probe"/>
              <w:tblPr>{tableProperties}
                <w:tblBorders>
                  <w:top w:val="single" w:sz="8"/><w:left w:val="single" w:sz="8"/>
                  <w:bottom w:val="single" w:sz="8"/><w:right w:val="single" w:sz="8"/>
                  <w:insideH w:val="single" w:sz="8"/><w:insideV w:val="single" w:sz="8"/>
                </w:tblBorders>
                <w:tblCellMar><w:left w:w="108" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar>
              </w:tblPr>
              {(firstRow is null ? "" : $"""<w:tblStylePr w:type="firstRow">{firstRow}</w:tblStylePr>""")}
              {(firstColumn is null ? "" : $"""<w:tblStylePr w:type="firstCol">{firstColumn}</w:tblStylePr>""")}
            </w:style>
            """;

    private static PageTable Table(
        string style,
        string look = """<w:tblLook w:firstRow="1" w:firstColumn="1" w:noVBand="1" w:noHBand="1"/>""",
        string firstCellProperties = "",
        string firstParagraphProperties = "")
    {
        string body = $"""
            <w:tbl>
              <w:tblPr><w:tblStyle w:val="Probe"/><w:tblW w:w="0" w:type="auto"/>{look}</w:tblPr>
              <w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="3000"/></w:tblGrid>
              <w:tr>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/>{firstCellProperties}</w:tcPr>
                  <w:p><w:pPr>{firstParagraphProperties}</w:pPr><w:r><w:t>Head</w:t></w:r></w:p></w:tc>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Two</w:t></w:r></w:p></w:tc>
              </w:tr>
              <w:tr>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Leaves</w:t></w:r></w:p></w:tc>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Body</w:t></w:r></w:p></w:tc>
              </w:tr>
              <w:tr>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Last</w:t></w:r></w:p></w:tc>
                <w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Row</w:t></w:r></w:p></w:tc>
              </w:tr>
            </w:tbl>
            <w:p/>
            """;

        return MiniDocx.Layout(body, style).Blocks.OfType<PageTable>().Single();
    }

    private static PageParagraph Paragraph(PageTableCell cell) => cell.Blocks.OfType<PageParagraph>().First();
}
