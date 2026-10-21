using XRay.Office.Core;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using XRay.Office.Presentations.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// <c>a:glow</c> and <c>a:softEdge</c>, rasterised as 26.8.1.1 rasterises them.
/// </summary>
/// <remarks>
/// <para>
/// <c>slide-glow-soft-edge.pptx</c>: a 157.5 × 94.5 pt rectangle with an 18 pt glow of red at 60%
/// alpha and the text "Glow text", one with a 20 pt soft edge and "Soft text", and one with an 8 pt
/// glow of <c>accent2</c>. 26.8.1.1 draws each glow as <strong>two</strong> images with soft masks
/// — 587 × 426 pixels reaching 36 pt out and 609 × 411 reaching 18 pt for the first — and the
/// softened rectangle as one 645 × 387 image, with the text drawn as text over all of them.
/// </para>
/// <para>
/// The values below are read out of the reference's own <c>/SMask</c> samples. The inner glow's
/// mask is reproduced exactly; the outer glow's and the soft edge's agree to within a pixel or two
/// of the bitmap, because the reference renders the inner bitmap into the outer's grid through
/// its own interpolation.
/// </para>
/// </remarks>
public class SlideGlowSoftEdgeTests
{
    private static (IReadOnlyList<PlacedShape> Shapes, DocSize Size) Slide()
    {
        using IDocument read = new PresentationReader().Read(
            DocumentSource.FromFile(Corpus.Require("slide-glow-soft-edge.pptx")));
        LaidOutSlide slide = ((SlidePages)((IPaginatedDocument)read).Layout()).Slides[0];
        return (slide.Shapes, slide.Size);
    }

    [Fact]
    public void ACustomShapesGlowIsTwoBitmapsTheSizesTheReferenceDraws()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide();
        PlacedShape shape = shapes.First(s => s.Glow is { Radius.Emu: 228600 });

        shape.Glow!.Value.Twice.ShouldBeTrue();
        shape.Glow!.Value.Colour.A.ShouldBe((byte)153);

        IReadOnlyList<SlideEffects.Layer> layers = SlideEffects.Glow(shape, shape.Glow!.Value, size);
        layers.Count.ShouldBe(2);

        (layers[0].Image.Width, layers[0].Image.Height).ShouldBe((587, 426));
        (layers[1].Image.Width, layers[1].Image.Height).ShouldBe((609, 411));

        // Reaching 36 pt and 18 pt beyond the 62.99 pt edge of the rectangle.
        layers[0].Destination.X.Points.ShouldBe(26.99, 0.1);
        layers[1].Destination.X.Points.ShouldBe(44.99, 0.1);
    }

    [Fact]
    public void TheInnerGlowsMaskIsTheReferencesOwn()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide();
        PlacedShape shape = shapes.First(s => s.Glow is { Radius.Emu: 228600 });

        RasterImage inner = SlideEffects.Glow(shape, shape.Glow!.Value, size)[1].Image;

        // Row 205, every fifth pixel from the left edge, read out of 26.8.1.1's /SMask.
        int[] reference = [0, 4, 12, 25, 42, 64, 89, 111, 129, 141, 150, 153];
        int[] ours = [.. Enumerable.Range(0, 12).Select(i => (int)Alpha(inner, i * 5, 205))];

        ours.ShouldBe(reference);
    }

    [Fact]
    public void ASoftEdgeReplacesTheFillWithOneFadedBitmap()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide();
        PlacedShape shape = shapes.Single(s => s.SoftEdge is not null);

        SlideEffects.Layer? softened = SlideEffects.SoftEdge(shape, shape.SoftEdge!.Value, size);
        softened.ShouldNotBeNull();

        RasterImage image = softened.Value.Image;
        (image.Width, image.Height).ShouldBe((645, 387));

        Alpha(image, 322, 193).ShouldBe((byte)255);
        Alpha(image, 0, 0).ShouldBe((byte)0);

        // The reference's row 193 at x = 0, 20 … 120 is 0 0 2 15 51 113 178: eroded twice by 20 pt
        // and blurred. Ours is within a few levels of it along the whole ramp.
        int[] reference = [0, 0, 2, 15, 51, 113, 178];
        for (int i = 0; i < reference.Length; i++)
        {
            ((int)Alpha(image, i * 20, 193)).ShouldBeInRange(reference[i] - 25, reference[i] + 25);
        }

        // And the text stays text.
        shape.Text.ShouldNotBeNull();
    }

    [Fact]
    public void AGlowOfAThemeColourIsOpaqueAtItsCore()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide();
        PlacedShape shape = shapes.First(s => s.Glow is { Radius.Emu: 101600 });

        IReadOnlyList<SlideEffects.Layer> layers = SlideEffects.Glow(shape, shape.Glow!.Value, size);
        layers.Count.ShouldBe(2);
        (layers[0].Image.Width, layers[0].Image.Height).ShouldBe((612, 409));
        Alpha(layers[1].Image, layers[1].Image.Width / 2, layers[1].Image.Height / 2).ShouldBe((byte)255);
    }

    private static byte Alpha(RasterImage image, int x, int y)
        => image.Pixels.Span[((y * image.Width) + x) * 4 + 3];
}
