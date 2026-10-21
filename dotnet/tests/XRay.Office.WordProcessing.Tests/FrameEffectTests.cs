using XRay.Office.Core.Documents;
using XRay.Office.Core.Graphics;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// <c>a:glow</c> and <c>a:softEdge</c> on a <c>wps:wsp</c>, which Writer draws as Impress does.
/// </summary>
/// <remarks>
/// <c>frame-glow.docx</c> and <c>frame-soft-edge.docx</c>: a filled 113.3 × 85 pt rectangle with an
/// 18 pt red glow at 60%, or a 20 pt soft edge. The daily 26.8 build draws the glow as two images,
/// 543 × 460 reaching 36 pt and 556 × 450 reaching 18 pt, and the soft edge as one in place of the
/// fill; nothing here drew either before.
/// </remarks>
public sealed class FrameEffectTests
{
    [Fact]
    public void ARectanglesGlowIsTwoBitmaps()
    {
        List<DrawnPicture> pictures = PicturesOf("frame-glow.docx");

        pictures.Count.ShouldBe(2);
        (pictures[0].Image.Width, pictures[0].Image.Height).ShouldBe((543, 460));
        (pictures[1].Image.Width, pictures[1].Image.Height).ShouldBe((556, 450));
        (pictures[1].Destination.X - pictures[0].Destination.X).Points.ShouldBe(18, 0.05);
    }

    [Fact]
    public void ASoftEdgeReplacesTheFill()
    {
        RecordingDrawingSink sink = Drawn("frame-soft-edge.docx");

        DrawnPicture softened = sink.Pages[0].Pictures.ShouldHaveSingleItem();
        softened.Destination.Width.Points.ShouldBe(113.3, 0.1);
        softened.Image.Pixels.Span[3].ShouldBe((byte)0);

        // The frame's own blue fill is not drawn as well.
        sink.Pages[0].Fills.OfType<SolidPaint>().ShouldNotContain(p => p.Colour == new Colour(0x44, 0x72, 0xC4));
    }

    private static List<DrawnPicture> PicturesOf(string name) => Drawn(name).Pages[0].Pictures;

    private static RecordingDrawingSink Drawn(string name)
    {
        RecordingDrawingSink sink = new();

        using DocumentSource source = DocumentSource.FromFile(Corpus.Require(name));
        using IDocument document = new WordProcessingReader().Read(source);

        IPageSequence pages = ((IPaginatedDocument)document).Layout();
        for (int i = 0; i < pages.Count; i++) pages[i].Draw(sink);

        return sink;
    }
}
