using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Units;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// Three readings of the RTF pandoc writes for a table, each of which made X-Ray.Office lay the table out
/// shorter than LibreOffice 26.8.1.1 does.
/// </summary>
/// <remarks>
/// Pandoc writes every cell as <c>{{\pard\intbl ... text\par}\cell}</c>, rules its header with a bare
/// <c>\clbrdrb\brdrs</c>, and emits no font table while still writing <c>\f0</c> on every run.
/// <c>rtf/table_error_codes.rtf</c> in <c>test_documents</c> has all three and was one page against
/// LibreOffice's two; with these readings it is two pages and <c>pdf-image-diff</c> reports both pages
/// <c>ok</c>.
/// </remarks>
public sealed class RtfPandocTableTests
{
    /// <summary>
    /// <c>text\par\cell</c> is two paragraphs: the cell mark always ends one, empty or not.
    /// </summary>
    /// <remarks>
    /// writerfilter buffers a <c>CellEnd</c> for every <c>\cell</c> after whatever <c>\par</c> ended
    /// (<c>rtfdispatchsymbol.cxx</c>:247-291). Measured: 26.8.1.1 exports a second, empty
    /// <c>Standard</c> paragraph in every cell of <c>table_error_codes.rtf</c>, and its rows are two
    /// lines tall.
    /// </remarks>
    [Fact]
    public void AParagraphEndedBeforeTheCellMarkLeavesAnEmptyOneBehindIt()
    {
        PageTableRow row = Row(@"\cellx4320\cellx8640", @"{{\pard\intbl Code\par}\cell}{{\pard\intbl Error\par}\cell}");

        row.Cells[0].Blocks.OfType<PageParagraph>().Select(p => p.Text).ShouldBe(["Code", string.Empty]);

        // And a cell whose text the cell mark itself ends stays one paragraph.
        Row(@"\cellx4320", @"\pard\intbl Code\cell")
            .Cells[0].Blocks.OfType<PageParagraph>().Select(p => p.Text).ShouldBe(["Code"]);
    }

    /// <summary>
    /// A cell border side exists when a style word describes it; with no <c>\brdrw</c> it is 0.75 pt.
    /// </summary>
    /// <remarks>
    /// Each width is the one 26.8.1.1's flat export states for the same declaration, and the row below
    /// each probe moved by exactly that much in both renderings.
    /// </remarks>
    [Theory]
    [InlineData(@"\clbrdrb\brdrs", 15)]
    [InlineData(@"\clbrdrb\brdrhair", 15)]
    [InlineData(@"\clbrdrb\brdrdot", 15)]
    [InlineData(@"\clbrdrb\brdrdb", 45)]
    [InlineData(@"\clbrdrb\brdrs\brdrw10", 10)]
    [InlineData(@"\clbrdrb\brdrs\brdrw40", 40)]
    [InlineData(@"\clbrdrb\brdrw20", 0)]
    [InlineData(@"\clbrdrb\brdrcf1", 0)]
    [InlineData(@"\clbrdrb\brdrs\brdrnone", 0)]
    public void ABorderNeedsAStyleWordAndDefaultsToThreeQuartersOfAPoint(string words, int twips)
    {
        PageTableRow row = Row(words + @"\cellx4320", @"\pard\intbl A\cell");

        row.Cells[0].Borders.Bottom.Width.ShouldBe(Length.FromTwips(twips));
        row.Cells[0].Borders.Top.IsNone.ShouldBeTrue();
    }

    /// <summary>
    /// A <c>\fN</c> the document has no font table entry for names a family nothing installs, and a run
    /// that says no <c>\f</c> keeps the default face.
    /// </summary>
    /// <remarks>
    /// 26.8.1.1 exports such a run as <c>fo:font-family="0"</c> and draws it in DejaVu Serif, the fallback
    /// for an unknown roman family, where the default face is Liberation Serif. The empty paragraph after a
    /// pandoc cell, which is outside the group that said <c>\f0</c>, is <c>Standard</c> and keeps it.
    /// </remarks>
    [Fact]
    public void AFontNumberWithNoTableEntryIsAFamilyNothingInstalls()
    {
        PageTableRow row = Row(@"\cellx4320", @"{{\pard\intbl \f0 Code\par}\cell}");
        PageParagraph[] paragraphs = [.. row.Cells[0].Blocks.OfType<PageParagraph>()];

        paragraphs[0].Font.ShouldNotBeNull().RequestedFamily.ShouldBe("0");
        paragraphs[1].Font?.RequestedFamily.ShouldNotBe("0");
    }

    private static PageTableRow Row(string declarations, string cells)
    {
        string rtf =
            @"{\rtf1\ansi{\colortbl;\red255\green0\blue0;}"
            + @"\paperw11906\paperh16838\margl1440\margr1440\margt1440\margb1440\sectd"
            + @"{\trowd\trgaph120" + declarations + @"\intbl{" + cells + @"}\intbl\row}"
            + @"\pard\plain After.\par}";

        using DocumentSource source = DocumentSource.FromStream(
            new MemoryStream(Encoding.ASCII.GetBytes(rtf)), "pandoc.rtf");
        using IDocument document = new WordProcessingReader().Read(source);
        WordProcessingPages pages = (WordProcessingPages)((IPaginatedDocument)document).Layout();

        return pages.Blocks.OfType<PageTable>().ShouldHaveSingleItem().Rows[0];
    }
}
