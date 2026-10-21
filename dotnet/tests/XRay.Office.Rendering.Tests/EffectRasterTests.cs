using XRay.Office.Core.Documents;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using XRay.Office.Rendering.Pdf;
using XRay.Office.Rendering.Raster;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Rendering.Tests;

/// <summary>
/// The rasteriser both backends lend through <see cref="IDrawingSink.Rasterise"/>, and the soft
/// edges and glows it makes possible on content whose pixels only a backend can produce.
/// </summary>
/// <remarks>
/// The document is <c>slide-soft-edge-content.pptx</c>: a 20 pt soft edge on a gradient
/// rectangle, on a rectangle with a 6 pt outline, on a picture and on an ellipse. The mask samples
/// below are read out of the daily 26.8 build's own <c>/SMask</c> streams for it.
/// </remarks>
public sealed class EffectRasterTests
{
    private static readonly PdfRenderOptions Reproducible = new()
    {
        CreationDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void BothBackendsRasteriseIntoTheStatedArea()
    {
        RasterImage? fromPdf = null;
        RasterImage? fromRaster = null;

        new PdfRenderer(Reproducible).Render(
            new DrawnPages(new DrawnPage(DrawnPage.A4, sink => fromPdf = HalfFilled(sink))), new MemoryStream());
        new RasterRenderer(new RasterRenderOptions { Dpi = 72 })
            .Rasterise(new DrawnPage(DrawnPage.A4, sink => fromRaster = HalfFilled(sink)))
            .Dispose();

        foreach (RasterImage? image in new[] { fromPdf, fromRaster })
        {
            image.ShouldNotBeNull();
            (image.Width, image.Height).ShouldBe((20, 10));

            // The left half of a 20 x 10 grid over 100..120 pt is the filled 100..110 pt, in
            // straight red; the right half is untouched and transparent.
            Pixel(image, 4, 5).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
            Pixel(image, 15, 5).A.ShouldBe((byte)0);
        }
    }

    [Fact]
    public void ASoftenedOutlineFadesItsStrokeWithItsFill()
    {
        PdfFile pdf = PdfFile.Parse(Render("slide-soft-edge-content.pptx"));

        // The outlined rectangle's bitmap covers the stroke's outer half too: 638 x 392, as the
        // reference's. Its mask is the reference's to the level along both edges.
        byte[] mask = Mask(pdf, 638, 392);
        Row(mask, 638, 196, 0, 70, 10).ShouldBe([0, 0, 0, 0, 3, 8, 17]);
        Column(mask, 638, 319, 0, 70, 10).ShouldBe([0, 0, 0, 1, 3, 9, 19]);
    }

    [Fact]
    public void ASoftenedPictureAndAGradientKeepTheirOwnPixels()
    {
        PdfFile pdf = PdfFile.Parse(Render("slide-soft-edge-content.pptx"));

        // Three 645 x 387 soft masks: the gradient, the picture and the ellipse. Before the
        // backend lent its rasteriser the gradient and the picture were drawn hard and only one
        // existed.
        List<byte[]> masks = Masks(pdf, 645, 387);
        masks.Count.ShouldBe(3);

        // The picture's (row 193, every twentieth pixel) and the ellipse's (every thirtieth), as
        // 26.8 writes them; the ellipse is eroded once, the custom shape's own pass alone.
        masks.ShouldContain(m => Row(m, 645, 193, 0, 140, 20).SequenceEqual(PictureRamp));
        masks.ShouldContain(m => Row(m, 645, 193, 0, 330, 30)
            .Zip(EllipseRamp)
            .All(pair => Math.Abs(pair.First - pair.Second) <= 1));
    }

    private static readonly int[] PictureRamp = [0, 9, 32, 70, 123, 178, 219];

    private static readonly int[] EllipseRamp = [0, 0, 5, 30, 84, 153, 211, 241, 252, 255, 255];

    private static RasterImage? HalfFilled(IDrawingSink sink)
        => sink.Rasterise(
            20, 10, new DocRect(Length.FromPoints(100), Length.FromPoints(100), Length.FromPoints(20), Length.FromPoints(10)),
            target => target.FillPath(
                GraphicsPath.Rectangle(new DocRect(
                    Length.FromPoints(100), Length.FromPoints(100), Length.FromPoints(10), Length.FromPoints(10))),
                Paint.Solid(new Colour(255, 0, 0))));

    private static (byte R, byte G, byte B, byte A) Pixel(RasterImage image, int x, int y)
    {
        ReadOnlySpan<byte> p = image.Pixels.Span[((y * image.Width + x) * 4)..];
        return (p[0], p[1], p[2], p[3]);
    }

    private static byte[] Render(string fileName)
    {
        using IDocument document = OfficeDocument.Open(Corpus.Require(fileName));
        IPageSequence pages = ((IPaginatedDocument)document).Layout();

        using MemoryStream output = new();
        new PdfRenderer(Reproducible).Render(pages, output);
        return output.ToArray();
    }

    private static List<byte[]> Masks(PdfFile pdf, int width, int height)
        => [.. pdf.Streams()
            .Where(s => s.Dictionary.Contains($"/Width {width}/Height {height}/ColorSpace/DeviceGray", StringComparison.Ordinal))
            .Select(s => s.Data)];

    private static byte[] Mask(PdfFile pdf, int width, int height) => Masks(pdf, width, height).ShouldHaveSingleItem();

    private static int[] Row(byte[] mask, int width, int y, int from, int to, int step)
        => [.. Enumerable.Range(0, (to - from) / step).Select(i => (int)mask[(y * width) + from + (i * step)])];

    private static int[] Column(byte[] mask, int width, int x, int from, int to, int step)
        => [.. Enumerable.Range(0, (to - from) / step).Select(i => (int)mask[((from + (i * step)) * width) + x])];
}
