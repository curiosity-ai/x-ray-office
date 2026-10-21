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
    [Fact]
    public void ACustomShapesGlowIsTwoBitmapsTheSizesTheReferenceDraws()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-soft-edge.pptx");
        PlacedShape shape = shapes.First(s => s.Glow is { Radius.Emu: 228600 });

        shape.Glow!.Value.Twice.ShouldBeTrue();
        shape.Glow!.Value.Colour.A.ShouldBe((byte)153);

        IReadOnlyList<EffectLayer> layers = SlideEffects.Glow(shape, shape.Glow!.Value, size);
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
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-soft-edge.pptx");
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
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-soft-edge.pptx");
        PlacedShape shape = shapes.Single(s => s.SoftEdge is not null);

        EffectLayer? softened = SlideEffects.SoftEdge(shape, shape.SoftEdge!.Value, size);
        softened.ShouldNotBeNull();

        RasterImage image = softened.Value.Image;
        (image.Width, image.Height).ShouldBe((645, 387));

        Alpha(image, 322, 193).ShouldBe((byte)255);
        Alpha(image, 0, 0).ShouldBe((byte)0);

        // The reference's row 193 at x = 0, 20 … 120: eroded twice by 20 pt and blurred. It is
        // the reference's to the level now that the radius is the model's 706 hundredths of a
        // millimetre and the discretes are the PDF device's 720 dpi; before, the ramp sat a pixel
        // out, a mean difference of 5.1 levels.
        int[] reference = [0, 0, 2, 15, 51, 113, 178];
        int[] ours = [.. Enumerable.Range(0, reference.Length).Select(i => (int)Alpha(image, i * 20, 193))];
        ours.ShouldBe(reference);

        // And the text stays text.
        shape.Text.ShouldNotBeNull();
    }

    [Fact]
    public void AGlowOfAThemeColourIsOpaqueAtItsCore()
    {
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-soft-edge.pptx");
        PlacedShape shape = shapes.First(s => s.Glow is { Radius.Emu: 101600 });

        IReadOnlyList<EffectLayer> layers = SlideEffects.Glow(shape, shape.Glow!.Value, size);
        layers.Count.ShouldBe(2);
        (layers[0].Image.Width, layers[0].Image.Height).ShouldBe((612, 409));
        Alpha(layers[1].Image, layers[1].Image.Width / 2, layers[1].Image.Height / 2).ShouldBe((byte)255);
    }

    [Fact]
    public void OnlyARectangleTakesItsEffectsTwice()
    {
        // `EnhancedCustomShape2d::CreateObject` renders a rectangle from an `SdrRectObj`, which
        // draws the effects again, and every other geometry from an `SdrPathObj`, which does not.
        // 26.8 draws one bitmap for an 18 pt glow on an ellipse and on a rounded rectangle, and
        // two on a rectangle.
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-ellipse.pptx");
        List<PlacedShape> glowing = [.. shapes.Where(s => s.Glow is not null)];
        glowing.Count.ShouldBe(3);

        glowing.Select(s => s.Glow!.Value.Twice).ShouldBe([false, false, true]);

        IReadOnlyList<EffectLayer> ellipse = SlideEffects.Glow(glowing[0], glowing[0].Glow!.Value, size);
        ellipse.Count.ShouldBe(1);
        (ellipse[0].Image.Width, ellipse[0].Image.Height).ShouldBe((609, 411));
    }

    [Fact]
    public void AGlowsShadowIsTheGlowRecoloured()
    {
        // `createEmbeddedShadowPrimitive` wraps the glow with the rest (tdf#132199): 26.8 draws both
        // glow bitmaps again, offset by the 20 pt shadow, in its colour.
        using IDocument read = new PresentationReader().Read(
            DocumentSource.FromFile(Corpus.Require("slide-glow-shadow.pptx")));
        RecordingDrawingSink sink = new();
        SlideDrawing.Draw(((SlidePages)((IPaginatedDocument)read).Layout()).Slides[0], sink);

        List<DrawnPicture> images = [.. sink.Pages[0].Pictures
            .Where(c => c.Image.Width == 587 && c.Image.Height == 426)];
        images.Count.ShouldBe(2);

        // The shadow's copy is first and black; it goes to the same rectangle, the 14.14 pt offset
        // being the shadow's transform.
        images[0].Destination.ShouldBe(images[1].Destination);
        images[0].Image.Pixels.Span[0..3].ToArray().ShouldBe([(byte)0, (byte)0, (byte)0]);
        images[1].Image.Pixels.Span[0..3].ToArray().ShouldBe([(byte)255, (byte)0, (byte)0]);
    }

    [Fact]
    public void TextGlowIsReadOnceForTheWholeBody()
    {
        (IReadOnlyList<PlacedShape> shapes, _) = Slide("slide-text-glow.pptx");

        // A 10 pt red glow at 60% on a 40 pt run, and a 5 pt green one on a 20 pt run; the shape's
        // own 18 pt counts with the runs when the radius is scaled.
        SlideTextGlow first = shapes[0].TextGlow!.Value;
        first.Radius.Emu.ShouldBe(127000);
        first.Colour.ShouldBe(new Colour(255, 0, 0, 153));
        first.ShapeFontSize.Points.ShouldBe(18, 0.01);

        shapes[1].TextGlow!.Value.Colour.ShouldBe(new Colour(0, 0xB0, 0x50));
    }

    [Fact]
    public void AnOdpStatesTheSameEffectsInItsGraphicStyles()
    {
        // LibreOffice's own conversion of slide-glow-soft-edge.pptx, which it renders image for
        // image as it renders the original.
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-glow-soft-edge.odp");

        PlacedShape glowing = shapes.First(s => s.Glow is { Radius.Emu: > 200000 });
        glowing.Glow!.Value.Twice.ShouldBeTrue();
        glowing.Glow!.Value.Colour.A.ShouldBe((byte)153);

        IReadOnlyList<EffectLayer> layers = SlideEffects.Glow(glowing, glowing.Glow!.Value, size);
        (layers[0].Image.Width, layers[0].Image.Height).ShouldBe((587, 426));
        (layers[1].Image.Width, layers[1].Image.Height).ShouldBe((609, 411));

        PlacedShape soft = shapes.Single(s => s.SoftEdge is not null);
        EffectLayer softened = SlideEffects.SoftEdge(soft, soft.SoftEdge!.Value, size)!.Value;
        (softened.Image.Width, softened.Image.Height).ShouldBe((645, 387));
        int[] ramp = [.. Enumerable.Range(0, 7).Select(i => (int)Alpha(softened.Image, i * 20, 193))];
        ramp.ShouldBe([0, 0, 2, 15, 51, 113, 178]);
    }

    [Fact]
    public void WithoutARasteriserAPictureIsNotSoftened()
    {
        // A picture's pixels need a codec, which only a backend has; a sink that cannot render
        // them leaves the picture drawn as it is rather than inventing its colours.
        (IReadOnlyList<PlacedShape> shapes, DocSize size) = Slide("slide-soft-edge-content.pptx");
        PlacedShape picture = shapes.Single(s => s.Picture is not null);

        SlideEffects.SoftEdge(picture, picture.SoftEdge!.Value, size).ShouldBeNull();
        picture.SoftEdge!.Value.Twice.ShouldBeFalse();
    }

    private static (IReadOnlyList<PlacedShape> Shapes, DocSize Size) Slide(string name)
    {
        using IDocument read = new PresentationReader().Read(DocumentSource.FromFile(Corpus.Require(name)));
        LaidOutSlide slide = ((SlidePages)((IPaginatedDocument)read).Layout()).Slides[0];
        return (slide.Shapes, slide.Size);
    }

    private static byte Alpha(RasterImage image, int x, int y)
        => image.Pixels.Span[((y * image.Width) + x) * 4 + 3];
}
