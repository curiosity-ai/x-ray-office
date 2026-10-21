using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// Text that overflows a row of exact height: the lines starting inside the cell are drawn and clipped to
/// it, and the ones starting below it are not drawn at all.
/// </summary>
/// <remarks>
/// Measured on this very shape — one 150 pt cell, a 20 pt exact row, five lines' worth of text — against
/// 26.8.1.1 and 26.2.4.2, which agree: 18 words in the text layer where this tree had 24, and the second
/// line's ink cut at the cell's bottom rule. See <c>TableLayouter.ClippedToExactRow</c>.
/// </remarks>
public sealed class ExactRowOverflowTests
{
    private const string Text =
        "Alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima mike november oscar papa";

    /// <summary>An exact row keeps the lines that start inside it and clips them.</summary>
    [Fact]
    public void AnExactRowKeepsOnlyTheLinesStartingInsideIt()
    {
        PlacedTableCell cell = FirstCell("exact");

        cell.ClipsContent.ShouldBeTrue();
        cell.Content.ShouldNotBeNull().Lines.Count.ShouldBe(2);
    }

    /// <summary>A floor is not a height: the row grows and nothing is clipped.</summary>
    [Fact]
    public void AnAtLeastRowGrowsInstead()
    {
        PlacedTableCell cell = FirstCell("atLeast");

        cell.ClipsContent.ShouldBeFalse();
        cell.Content.ShouldNotBeNull().Lines.Count.ShouldBeGreaterThan(2);
    }

    private static PlacedTableCell FirstCell(string rule)
    {
        string body = $"""
            <w:tbl>
              <w:tblPr><w:tblW w:w="3000" w:type="dxa"/></w:tblPr>
              <w:tblGrid><w:gridCol w:w="3000"/></w:tblGrid>
              <w:tr><w:trPr><w:trHeight w:val="400" w:hRule="{rule}"/></w:trPr>
                <w:tc><w:p><w:r><w:t>{Text}</w:t></w:r></w:p></w:tc></w:tr>
              <w:tr><w:tc><w:p><w:r><w:t>Second row</w:t></w:r></w:p></w:tc></w:tr>
            </w:tbl>
            <w:p/>
            """;

        return MiniDocx.Layout(body).Pages[0].Tables.SelectMany(table => table.Cells).First();
    }
}
