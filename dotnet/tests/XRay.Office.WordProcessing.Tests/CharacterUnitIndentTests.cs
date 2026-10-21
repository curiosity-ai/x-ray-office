using XRay.Office.Core.Units;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// DOCX indents stated in characters (<c>w:leftChars</c>, <c>w:rightChars</c>, <c>w:firstLineChars</c>,
/// <c>w:hangingChars</c>) and paragraph spacing stated in lines (<c>w:beforeLines</c>, <c>w:afterLines</c>).
/// </summary>
/// <remarks>
/// Every expected value is what 26.8's daily draws on <c>probes/words-indentchars/</c>: twenty-five
/// one-paragraph or one-table documents, each varying one attribute, in a 10.5 pt default — so a character is
/// 210 twips and two of them the 21 pt the reference indents by. This tree agrees with all of them to the two
/// writers' constant 0.1 pt origin offset.
/// </remarks>
public sealed class CharacterUnitIndentTests
{
    [Theory]
    [InlineData("""w:leftChars="200" """, 420, 0, 0)]
    [InlineData("""w:leftChars="200" w:left="720" """, 420, 0, 0)]
    [InlineData("""w:leftChars="0" w:left="720" """, 720, 0, 0)]
    [InlineData("""w:rightChars="200" """, 0, 420, 0)]
    [InlineData("""w:rightChars="0" w:right="374" """, 0, 374, 0)]
    [InlineData("""w:right="374" w:rightChars="0" """, 0, 374, 0)]
    [InlineData("""w:firstLineChars="200" """, 0, 0, 420)]
    [InlineData("""w:firstLineChars="0" w:firstLine="720" """, 0, 0, 720)]
    // tdf#167721: a hanging count ignores w:left and becomes the left indent itself.
    [InlineData("""w:hangingChars="100" w:left="720" """, 210, 0, -210)]
    public void ACountBeatsTwipsAndANoughtDisablesIt(string ind, long start, long end, long first)
    {
        PageParagraph paragraph = Paragraph($"<w:ind {ind}/>");

        paragraph.Format.StartIndent.ShouldBe(Length.FromTwips(start));
        paragraph.Format.EndIndent.ShouldBe(Length.FromTwips(end));
        paragraph.Format.FirstLineIndent.ShouldBe(Length.FromTwips(first));
    }

    /// <summary>
    /// A character is the paragraph style's size, not its runs': a 16 pt run in a 10.5 pt style still indents
    /// two characters by 21 pt, and a 20 pt style by 40.
    /// </summary>
    [Fact]
    public void ACharacterIsTheStylesSize()
    {
        Paragraph("""<w:ind w:leftChars="200"/>""", run: """<w:sz w:val="32"/>""")
            .Format.StartIndent.ShouldBe(Length.FromTwips(420));
        Paragraph("""<w:pStyle w:val="Big"/><w:ind w:leftChars="200"/>""")
            .Format.StartIndent.ShouldBe(Length.FromTwips(800));
    }

    /// <summary>An inherited count beats the paragraph's own twips; the paragraph's nought gives the style's twips back.</summary>
    [Fact]
    public void AStylesCountIsInherited()
    {
        Paragraph("""<w:pStyle w:val="Ch"/>""").Format.StartIndent.ShouldBe(Length.FromTwips(630));
        Paragraph("""<w:pStyle w:val="Ch"/><w:ind w:left="720"/>""").Format.StartIndent.ShouldBe(Length.FromTwips(630));
        Paragraph("""<w:pStyle w:val="Ch"/><w:ind w:leftChars="0"/>""").Format.StartIndent.ShouldBe(Length.FromTwips(200));
    }

    /// <summary>
    /// A table style's nought count is applied raw and zeroes the indent, where a paragraph style's keeps the
    /// twips: the right-aligned first column of <c>docx/word_sample.docx</c>'s <c>Report Table</c>.
    /// </summary>
    [Theory]
    [InlineData("TZero", 0L)]
    [InlineData("TPlain", 374L)]
    [InlineData("TChars", 420L)]
    public void ATableStylesNoughtZeroesTheIndent(string tableStyle, long end)
    {
        string body = $"""
            <w:tbl><w:tblPr><w:tblStyle w:val="{tableStyle}"/><w:tblW w:w="3000" w:type="dxa"/></w:tblPr>
              <w:tblGrid><w:gridCol w:w="3000"/></w:tblGrid>
              <w:tr><w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr><w:p><w:r><w:t>Cell</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
            <w:p/>
            """;

        PageParagraph cell = MiniDocx.Layout(body, Styles).Pages[0].Tables.SelectMany(placed => placed.Cells)
            .SelectMany(placed => placed.Content!.Blocks).OfType<PageParagraph>().First();

        cell.Format.EndIndent.ShouldBe(Length.FromTwips(end));
    }

    [Fact]
    public void AParagraphStylesNoughtKeepsItsTwips()
        => Paragraph("""<w:pStyle w:val="RZero"/>""").Format.EndIndent.ShouldBe(Length.FromTwips(374));

    /// <summary>A line is a fixed 240 twips; a stated <c>w:before</c> wins in either attribute order.</summary>
    [Theory]
    [InlineData("""w:beforeLines="100" """, 240L, 0L)]
    [InlineData("""w:beforeLines="33" """, 79L, 0L)]
    [InlineData("""w:before="0" w:beforeLines="100" """, 0L, 0L)]
    [InlineData("""w:beforeLines="100" w:before="0" """, 0L, 0L)]
    [InlineData("""w:afterLines="50" """, 0L, 120L)]
    public void SpacingInLinesIsTwoHundredAndFortyTwipsALine(string spacing, long before, long after)
    {
        PageParagraph paragraph = Paragraph($"<w:spacing {spacing}/>");

        paragraph.Format.SpaceBefore.ShouldBe(Length.FromTwips(before));
        paragraph.Format.SpaceAfter.ShouldBe(Length.FromTwips(after));
    }

    private static PageParagraph Paragraph(string pPr, string run = "")
        => MiniDocx.Layout($"<w:p><w:pPr>{pPr}</w:pPr><w:r><w:rPr>{run}</w:rPr><w:t>Text</w:t></w:r></w:p>", Styles)
            .Blocks.OfType<PageParagraph>().First();

    private static string TableStyle(string id, string ind) => $"""
        <w:style w:type="table" w:styleId="{id}"><w:name w:val="{id}"/><w:pPr><w:ind {ind}/></w:pPr></w:style>
        """;

    private static readonly string Styles = """
        <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/>
          <w:sz w:val="21"/></w:rPr></w:rPrDefault>
          <w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
        <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
        <w:style w:type="paragraph" w:styleId="Big"><w:name w:val="Big"/><w:basedOn w:val="Normal"/><w:rPr><w:sz w:val="40"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Ch"><w:name w:val="Ch"/><w:basedOn w:val="Normal"/><w:pPr><w:ind w:leftChars="300" w:left="200"/></w:pPr></w:style>
        <w:style w:type="paragraph" w:styleId="RZero"><w:name w:val="RZero"/><w:basedOn w:val="Normal"/><w:pPr><w:ind w:rightChars="0" w:right="374"/></w:pPr></w:style>
        """
        + TableStyle("TZero", """w:rightChars="0" w:right="374" """)
        + TableStyle("TPlain", """w:right="374" """)
        + TableStyle("TChars", """w:rightChars="200" w:right="374" """);
}
