using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// The character half of DOCX's <c>w:docGrid</c>: how its pitch is computed, and what it does to a line.
/// </summary>
/// <remarks>
/// <para>
/// The pitch is <c>SectionPropertyMap::CloseSectionGroup</c>'s arithmetic
/// (<c>sw/source/writerfilter/dmapper/PropertyMap.cxx</c>:1958-2035). Every expected value was read back off
/// 26.8's daily (LibreOfficeDev 26.8.3.0.0 9dcd8a837588) by the character advance it draws on
/// <c>probes/words-chargrid/</c>, ten section variants of one document, 10.5 pt WenQuanYi Zen Hei: 11.5 pt
/// at <c>charSpace</c> 4096, 9.65 at −3426, 20.5 at 40960, 11.0 at 2048, 13.0 with a 12 pt <c>Normal</c>
/// at 4096, and two cells of 9.65 under <c>snapToChars</c> at −3426. At every variant this tree now breaks
/// every line where the reference does.
/// </para>
/// </remarks>
public sealed class CharacterGridDocxTests
{
    [Theory]
    [InlineData("linesAndChars", -3426, 193, false)]
    [InlineData("linesAndChars", 4096, 230, false)]
    [InlineData("linesAndChars", 40960, 410, false)]
    [InlineData("linesAndChars", 2048, 220, false)]
    [InlineData("snapToChars", 4096, 230, true)]
    [InlineData("snapToChars", -3426, 193, true)]
    public void ThePitchIsTheDocumentFontPlusTheCharacterSpace(string type, int charSpace, long width, bool snaps)
    {
        CharacterGrid grid = Grid($"""<w:docGrid w:type="{type}" w:linePitch="312" w:charSpace="{charSpace}"/>""")
            .ShouldNotBeNull();

        grid.Width.ShouldBe(Length.FromTwips(width));
        grid.BaseFontHeight.ShouldBe(Length.FromTwips(210));
        grid.SnapsToCharacters.ShouldBe(snaps);
    }

    /// <summary>
    /// A grid of lines, or a character grid stating no character space, has no character half: writerfilter
    /// turns <c>LINES_AND_CHARS</c> without <c>m_nDxtCharSpace</c> into <c>LINES</c>.
    /// </summary>
    [Theory]
    [InlineData("""<w:docGrid w:type="lines" w:linePitch="312" w:charSpace="4096"/>""")]
    [InlineData("""<w:docGrid w:type="linesAndChars" w:linePitch="312" w:charSpace="0"/>""")]
    [InlineData("""<w:docGrid w:type="snapToChars" w:linePitch="312"/>""")]
    public void WithoutACharacterSpaceThereIsNoCharacterGrid(string docGrid)
        => Grid(docGrid).ShouldBeNull();

    /// <summary>The <c>Normal</c> style's own size overrides the defaults' (tdf#107359), and sets the base.</summary>
    [Fact]
    public void TheNormalStylesSizeDecides()
    {
        CharacterGrid grid = Grid(
                """<w:docGrid w:type="linesAndChars" w:linePitch="312" w:charSpace="4096"/>""",
                normal: """<w:rPr><w:sz w:val="24"/></w:rPr>""")
            .ShouldNotBeNull();

        grid.Width.ShouldBe(Length.FromTwips(260));
        grid.BaseFontHeight.ShouldBe(Length.FromTwips(240));
    }

    /// <summary>A line pitch under 100 twips is raised to it by <c>MIN_TEXTGRID_SIZE</c>.</summary>
    [Fact]
    public void ALinePitchIsAtLeastOneHundredTwips()
        => Paragraphs("""<w:docGrid w:type="lines" w:linePitch="50"/>""")[0].Format.TextGrid
            .ShouldNotBeNull().Pitch.ShouldBe(Length.FromTwips(100));

    /// <summary>
    /// A justified paragraph on a <c>snapToChars</c> grid is not stretched: no portion has a blank to give
    /// the slack to. The reference draws such a line at its natural width; under <c>linesAndChars</c> it
    /// stretches it as it always does.
    /// </summary>
    [Fact]
    public void SnapToCharsJustifiesNothing()
    {
        const string Body = """
            <w:p><w:pPr><w:jc w:val="both"/></w:pPr><w:r><w:t>Short words then a long one PneumonoultramicroscopicsilicovolcanoconiosisPneumonoultramicroscopicsilicovolcanoconiosis end</w:t></w:r></w:p>
            """;

        PlacedLine Snapped(string type) => MiniDocx.Layout(
                Body, Styles(""),
                sectionProperties: Section($"""<w:docGrid w:type="{type}" w:linePitch="312" w:charSpace="4096"/>"""))
            .Pages[0].Lines[0];

        Snapped("snapToChars").Box.SpaceAdd.ShouldBe(Length.Zero);
        Snapped("linesAndChars").Box.SpaceAdd.ShouldBeGreaterThan(Length.Zero);
    }

    private static CharacterGrid? Grid(string docGrid, string normal = "")
        => Paragraphs(docGrid, normal)[0].Format.TextGrid?.Characters;

    private static List<PageParagraph> Paragraphs(string docGrid, string normal = "")
        => [.. MiniDocx.Layout(
                "<w:p><w:r><w:t>永和九年</w:t></w:r></w:p>", Styles(normal), sectionProperties: Section(docGrid))
            .Blocks.OfType<PageParagraph>()];

    private static string Styles(string normal) => $"""
        <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/>
          <w:sz w:val="21"/></w:rPr></w:rPrDefault></w:docDefaults>
        <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/>{normal}</w:style>
        """;

    private static string Section(string grid) => $"""
        <w:pgSz w:w="12240" w:h="15840"/>
        <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
        {grid}
        """;
}
