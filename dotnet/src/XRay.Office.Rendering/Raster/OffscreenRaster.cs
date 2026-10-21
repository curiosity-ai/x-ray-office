using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using SkiaSharp;

namespace XRay.Office.Rendering.Raster;

/// <summary>
/// The rasteriser every backend lends through <see cref="IDrawingSink.Rasterise"/>: drawing
/// commands rendered into a transparent bitmap of a stated size.
/// </summary>
/// <remarks>
/// <para>
/// One implementation for the raster and the PDF backend alike, because what it serves — a soft
/// edge's or a glow's content, see <see cref="ShapeEffects"/> — is a bitmap in the reference's
/// output whichever format that is, and the two backends must hand the effect the same pixels or
/// a PDF and a PNG of one slide would fade differently.
/// </para>
/// <para>
/// The area maps onto the bitmap by a scale per axis and a translation. The scale is carried
/// partly as the sink's resolution, which is how <see cref="SkiaDrawingSink"/> already turns EMUs
/// into pixels, and the rest as a canvas matrix: x needs nothing more, y the ratio of the two
/// scales, both the translation to the area's corner.
/// </para>
/// </remarks>
internal static class OffscreenRaster
{
    /// <summary>Renders <paramref name="draw"/> into a <paramref name="width"/> × <paramref name="height"/> bitmap.</summary>
    public static RasterImage? Render(int width, int height, DocRect area, Action<IDrawingSink> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        if (width <= 0 || height <= 0 || area.Width <= Length.Zero || area.Height <= Length.Zero) return null;
        if ((long)width * height > 64L * 1024 * 1024) return null;

        double perEmuX = width / (double)area.Width.Emu;
        double perEmuY = height / (double)area.Height.Emu;

        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(new SKMatrix(
                1, 0, (float)(-area.X.Emu * perEmuX),
                0, (float)(perEmuY / perEmuX), (float)(-area.Y.Emu * perEmuY),
                0, 0, 1));

            RasterRenderOptions options = new()
            {
                Dpi = perEmuX * Length.EmuPerInch,
                Antialias = true,
                Background = Colour.Transparent,
            };

            using SkiaDrawingSink sink = new(canvas, options);
            draw(sink);
            canvas.Flush();
        }

        // Skia draws premultiplied; a RasterImage is straight.
        byte[] pixels = bitmap.Bytes;
        for (int i = 0; i + 3 < pixels.Length; i += 4)
        {
            int a = pixels[i + 3];
            if (a is 0 or 255) continue;

            pixels[i] = (byte)Math.Min(255, (pixels[i] * 255 + a / 2) / a);
            pixels[i + 1] = (byte)Math.Min(255, (pixels[i + 1] * 255 + a / 2) / a);
            pixels[i + 2] = (byte)Math.Min(255, (pixels[i + 2] * 255 + a / 2) / a);
        }

        return new RasterImage { Width = width, Height = height, Pixels = pixels };
    }
}
