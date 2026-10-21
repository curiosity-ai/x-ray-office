using XRay.Office.Core.Documents;
using XRay.Office.Core.Graphics;
using XRay.Office.Spreadsheets.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Spreadsheets.Tests;

/// <summary>
/// <c>a:glow</c> and <c>a:softEdge</c> on a worksheet shape, which Calc draws as Impress does.
/// </summary>
/// <remarks>
/// <c>sheet-shape-glow.xlsx</c> and <c>sheet-shape-soft-edge.xlsx</c>: one filled rectangle
/// <c>xdr:sp</c> with an 18 pt red glow at 60%, or a 20 pt soft edge. The daily 26.8 build draws the
/// glow as two images, 854 × 293 and, clipped 18 pt left of the left margin, 759 × 329, and the
/// soft edge as one; nothing here drew either before.
/// </remarks>
public sealed class SheetShapeEffectTests
{
    [Fact]
    public void AGlowIsTwoBitmapsTheOuterClippedAtThePagesBody()
    {
        List<DrawnPicture> pictures = PicturesOf("sheet-shape-glow.xlsx");

        pictures.Count.ShouldBe(2);
        (pictures[0].Image.Width, pictures[0].Image.Height).ShouldBe((759, 329));
        (pictures[1].Image.Width, pictures[1].Image.Height).ShouldBe((854, 293));

        // The left margin is 0.7875 in, and the clip is the body grown by the radius.
        pictures[0].Destination.X.Points.ShouldBe(56.70 - 18, 0.1);
        pictures[1].Image.Pixels.Span[3].ShouldBe((byte)0);
    }

    [Fact]
    public void ASoftEdgeReplacesTheFill()
    {
        List<DrawnPicture> pictures = PicturesOf("sheet-shape-soft-edge.xlsx");

        DrawnPicture softened = pictures.ShouldHaveSingleItem();
        softened.Destination.Width.Points.ShouldBe(206.1, 0.1);

        // A 47 pt tall shape eroded twice by 20 pt is faint all through: the reference's centre is
        // alpha 27, ours 26.
        RasterImage image = softened.Image;
        ((int)image.Pixels.Span[((image.Height / 2 * image.Width) + (image.Width / 2)) * 4 + 3]).ShouldBeInRange(24, 30);
        image.Pixels.Span[3].ShouldBe((byte)0);
    }

    private static List<DrawnPicture> PicturesOf(string name)
    {
        using IPaginatedDocument document = (IPaginatedDocument)OfficeDocument.Open(Corpus.Require(name));

        SpreadsheetPages pages = (SpreadsheetPages)document.Layout();
        RecordingDrawingSink sink = new();
        sink.BeginPage(pages[0].Size);
        pages[0].Draw(sink);
        sink.EndPage();

        return sink.Pages[0].Pictures;
    }
}
