using XRay.Office.Core.Documents;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Spreadsheets.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Spreadsheets.Tests;

/// <summary>
/// A <c>twoCellAnchor editAs="oneCell"</c> keeps the object's own stated size, for a chart and for
/// a quarter-turned shape, and the print area covers what is drawn.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A chart's size was never found.</strong> <c>XlsxDrawings.Transform</c> looked for the
/// stated size under <c>spPr</c>, where a graphic frame has none: its <c>xdr:xfrm</c> is a direct
/// child of <c>xdr:graphicFrame</c> (<c>oox/source/drawingml/graphicshapecontext.cxx</c>:161). So
/// the <c>editAs</c> rule (<c>sc/source/filter/oox/drawingfragment.cxx</c>:284-295) never fired for
/// a chart and it was drawn from cell to cell. On <c>xlsx/test_01.xlsm</c> that was 443 pt where
/// 26.8.1.1 draws 326.2; <c>features/sheet-chart-editas-onecell.xlsx</c> states 3 × 2 inches across
/// nine columns and 26.8.1.1 draws its chart area 215.2 × 143.2 pt.
/// </para>
/// <para>
/// <strong>A quarter-turned shape's stated size is exchanged first</strong> (cool#16182,
/// 6e9467abfb46, in 26.8.1.1 and not in 26.2.4.2). <c>features/sheet-drawing-rot90-onecell.xlsx</c>
/// is a red bar stated 3000000 × 1000000 EMU at <c>rot="5400000"</c>: 26.8.1.1 stands it up, 157 ×
/// 472 px at 144 dpi from (301, 204), and this tree now draws the same pixels.
/// </para>
/// <para>
/// <strong>And the print area is taken round the turned shape.</strong> Before either fix the bar
/// lay down and was clipped to 192 pt where 26.2.4.2 prints all 236: <c>SheetDrawingBounds</c>
/// measured the anchor unreflected while <c>SheetPageGraphics</c> drew it reflected, so the block
/// and the drawing were two different rectangles.
/// </para>
/// </remarks>
public sealed class SheetEditAsAnchorTests
{
    private static SpreadsheetPages Layout(string name)
    {
        using IPaginatedDocument document =
            (IPaginatedDocument)OfficeDocument.Open(Corpus.Require(name));

        return (SpreadsheetPages)document.Layout();
    }

    [Fact]
    public void AChartAnchoredEditAsOneCellKeepsItsStatedSize()
    {
        SheetDrawing chart = Layout("sheet-chart-editas-onecell.xlsx").Sheets[0].Drawings.Items
            .Single(drawing => drawing.IsChart);

        chart.Anchor.ShouldBe(SheetAnchorKind.OneCell, "editAs oneCell keeps the frame's own size");
        chart.Extent.Width.Points.ShouldBe(216, 0.01);
        chart.Extent.Height.Points.ShouldBe(144, 0.01);
    }

    [Fact]
    public void AQuarterTurnedOneCellShapeHasItsStatedSizeExchanged()
    {
        SheetDrawing bar = Layout("sheet-drawing-rot90-onecell.xlsx").Sheets[0].Drawings.Items
            .Single();

        bar.QuarterTurnedAnchor.ShouldBeTrue();
        bar.Extent.Width.Emu.ShouldBe(1000000, "the reflection exchanges them back when drawn");
        bar.Extent.Height.Emu.ShouldBe(3000000);
    }

    [Fact]
    public void AQuarterTurnedOneCellShapeStandsUpAndIsNotClipped()
    {
        SpreadsheetPages pages = Layout("sheet-drawing-rot90-onecell.xlsx");
        PlacedDrawingSink sink = new();
        pages.Pages[0].Draw(sink);

        List<DocRect> red = [.. sink.Fills
            .Where(fill => fill.Paint is SolidPaint { Colour: var colour }
                           && colour == Colour.FromRgb(0xFF0000))
            .Select(fill => fill.Bounds)];
        red.Count.ShouldBe(1, string.Join(
            " | ", red.Select(r => $"{r.X.Points:0.00},{r.Y.Points:0.00} {r.Width.Points:0.00}x{r.Height.Points:0.00}")));
        DocRect bar = red[0];

        // 26.8.1.1: 78.5 x 236.0 pt at (150.5, 102.0) from the top-left, read off its own raster.
        bar.Width.Points.ShouldBe(78.74, 0.05, "stood up: the stated height is its width");
        bar.Height.Points.ShouldBe(236.22, 0.05);

        foreach (DocRect clip in sink.Clips)
        {
            bool covers = clip.Left <= bar.Left && clip.Top <= bar.Top
                          && clip.Right >= bar.Right && clip.Bottom >= bar.Bottom;
            covers.ShouldBeTrue(
                "the printed block is taken round the turned shape, so no clip cuts it");
        }
    }
}
