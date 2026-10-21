using XRay.Office.Core.Geometry;
using XRay.Office.Core.Units;

namespace XRay.Office.Core.Graphics;

/// <summary>One bitmap an effect is drawn as, and where it goes in the drawing's coordinates.</summary>
/// <param name="Image">The bitmap: straight RGBA, its alpha the effect's mask.</param>
/// <param name="Destination">The rectangle the bitmap is stretched onto.</param>
public readonly record struct EffectLayer(RasterImage Image, DocRect Destination);

/// <summary>A shape's glow, for a host that has no model of its own for one.</summary>
/// <param name="Colour">The glow's colour, its alpha the glow's opacity.</param>
/// <param name="Radius">How far the halo reaches beyond the shape.</param>
/// <param name="Twice">
/// Whether it is applied twice: on a rectangle custom shape, whose engine renders it from an
/// <c>SdrRectObj</c> that carries the same effect (see <see cref="ShapeEffects.Glow"/>).
/// </param>
public readonly record struct ShapeGlow(Colour Colour, Length Radius, bool Twice);

/// <summary>A shape's soft edge, for a host that has no model of its own for one.</summary>
/// <param name="Radius">How far in from the outline the shape fades.</param>
/// <param name="Twice">Whether it is applied twice, for the reason <see cref="ShapeGlow.Twice"/> gives.</param>
public readonly record struct ShapeSoftEdge(Length Radius, bool Twice);

/// <summary>
/// What a glow or a soft edge is computed from: the drawing it surrounds or fades.
/// </summary>
/// <remarks>
/// <para>
/// LibreOffice computes both from the <em>pixels</em> of their content — the children of the
/// <c>GlowPrimitive2D</c> or <c>SoftEdgePrimitive2D</c> rendered into a bitmap — so the faithful
/// input is a drawing, <see cref="Draw"/>, that a backend renders for us through
/// <see cref="IDrawingSink.Rasterise"/>. Where the content is plain geometry its pixels can be
/// computed without a backend, from <see cref="Fill"/> and <see cref="Stroke"/>, and that path is
/// kept for the case it covers exactly: it reproduced 26.8.1.1's inner-glow mask to the level on
/// <c>slide-glow-soft-edge.pptx</c>, and it is what a sink with no rasteriser (a test's recorder,
/// a metafile's display list) still gets.
/// </para>
/// </remarks>
public sealed class EffectContent
{
    /// <summary>The filled areas, in the drawing's coordinates; empty when nothing is filled.</summary>
    public IReadOnlyList<GraphicsPath> Fill { get; init; } = [];

    /// <summary>The stroked outlines; empty when nothing is stroked.</summary>
    public IReadOnlyList<GraphicsPath> Stroke { get; init; } = [];

    /// <summary>The width of <see cref="Stroke"/>; zero is a hairline.</summary>
    public Length StrokeWidth { get; init; }

    /// <summary>
    /// The colour the whole content is, when it is one colour of fill and nothing else — the case
    /// a soft edge can be computed for without its pixels.
    /// </summary>
    public Colour? SolidColour { get; init; }

    /// <summary>
    /// Draws the content as it is drawn on the page, for an effect that needs its pixels: a
    /// picture, a gradient, an outlined shape, text. Null when the geometry above is the whole of
    /// it.
    /// </summary>
    public Action<IDrawingSink>? Draw { get; init; }

    /// <summary>
    /// Whether the mask is taken from <see cref="Draw"/>'s pixels even where the geometry could
    /// give it — true for content the geometry cannot describe (text, a picture with transparent
    /// pixels).
    /// </summary>
    public bool MaskFromPixels { get; init; }

    /// <summary>
    /// The content's range when it is not the geometry's: text's is its ink, found by rendering
    /// it. Null to take it from <see cref="Fill"/> and <see cref="Stroke"/>.
    /// </summary>
    public DocRect? Bounds { get; init; }
}

/// <summary>
/// Glow and soft edge, rasterised the way LibreOffice 26.8 rasterises them, for every host that
/// draws a DrawingML or ODF shape — a slide, a Writer page, a worksheet.
/// </summary>
/// <remarks>
/// <para>
/// Both are bitmaps in the reference's PDF, never vector: <c>GlowPrimitive2D</c> and
/// <c>SoftEdgePrimitive2D</c> (<c>drawinglayer/source/primitive2d/glowprimitive2d.cxx</c>,
/// <c>softedgeprimitive2d.cxx</c> on the 26.8 branch) render their content to an alpha mask,
/// process it and decompose to a <c>BitmapPrimitive2D</c>, which the PDF export writes as an image
/// with an <c>/SMask</c>. The effects are attributes of any <c>SdrObject</c> and are added by
/// <c>svx</c>'s shape primitives (<c>sdrcustomshapeprimitive2d.cxx</c>:39-52,
/// <c>sdrgrafprimitive2d.cxx</c>:95-130, <c>sdrrectangleprimitive2d.cxx</c>:96-124), which every
/// application shares — so Impress, Writer and Calc draw them alike, and the code lives here
/// rather than in one of them. It depends on nothing above this library.
/// </para>
/// <para>
/// The steps are the reference's, in its order:
/// </para>
/// <list type="number">
///   <item><description>The content's range, grown by the glow's radius (a soft edge's is not
///   grown), clipped to the visible area grown by the radius (twice the radius for a soft
///   edge).</description></item>
///   <item><description>Its size in the view's discrete units, which for the PDF export are
///   hundredths of a millimetre, reduced to at most 250 000 pixels by a uniform factor
///   (<c>implPrepareConversion</c>, <c>drawinglayer/source/tools/converters.cxx</c>:44-70).</description></item>
///   <item><description>The content rendered into that grid, of which only the fully transparent
///   pixels count as outside (<c>CreateMask(COL_WHITE)</c>).</description></item>
///   <item><description><c>ProcessAndBlurAlphaMask</c>
///   (<c>GlowSoftEgdeShadowTools.cxx</c>): halve while the blur exceeds 254 pixels or a side
///   exceeds 1000, dilate (glow, by half the radius) or erode (soft edge, by the radius, the
///   outside counting as transparent) with a square structuring element, give the inside the
///   glow's transparency, stack-blur (half the radius for a glow, the radius for a soft edge),
///   and scale back.</description></item>
///   <item><description>A glow is its colour under that mask; a soft edge is the content's own
///   pixels under the product of their alpha and the mask (<c>AlphaMask::BlendWith</c>,
///   <c>vcl/source/bitmap/alpha.cxx</c>:93-140).</description></item>
/// </list>
/// <para>
/// <strong>The content's pixels come from the backend.</strong> A soft edge keeps its content's
/// colours, so softening a picture, a gradient or an outlined shape needs those rendered — and
/// rendering lives in <c>XRay.Office.Rendering</c>, which nothing here may reach. The backend lends
/// its rasteriser through <see cref="IDrawingSink.Rasterise"/> instead, and this class keeps the
/// grid, the morphology and the blur, which have to be bit-exact and are the same for every
/// backend. A sink that cannot rasterise leaves such content drawn as it always was.
/// </para>
/// <para>
/// What is not reproduced: the reference's own rasteriser's anti-aliasing and its JPEG encoding of
/// the colour plane, so the edges agree to a pixel or two of the bitmap rather than exactly.
/// </para>
/// </remarks>
public static class ShapeEffects
{
    private const int MaximumQuadraticPixels = 250_000;

    private const double EmuPerMm100 = 360.0;

    /// <summary>
    /// Discrete pixels per hundredth of a millimetre: the view the PDF export decomposes in.
    /// </summary>
    /// <remarks>
    /// The object coordinates are the model's hundredths of a millimetre, but the view that turns
    /// them into pixels is the PDF writer's own device, which is a 720 dpi reference device
    /// (<c>RefDevMode::PDF1</c>, <c>vcl/source/gdi/virdev.cxx</c>:410-412, set at
    /// <c>vcl/source/pdf/pdfwriter_impl.cxx</c>:423). It decides the grid wherever the 250 000
    /// pixel cap does not — a glow of small text, a small shape — and, through the ceilings, the
    /// radius and the grid's exact aspect wherever it does. Measured: a 157.5 × 94.5 pt softened
    /// rectangle is 645 × 387 pixels, which 1/100 mm discretes give for the PPTX's 5555.6 ×
    /// 3333.3 and not for the ODP's 5555 × 3332, while 720 dpi gives it for both, and the masks
    /// then agree to the level; 12 pt text with a 47-unit glow is a 690 × 139 grid at 720 dpi, its
    /// halo a seventh of a point wider than the capped 1/100 mm grid made it.
    /// </remarks>
    private const double DiscretePerUnit = 720.0 / 2540.0;

    /// <summary>The bitmaps a glow is drawn as, outermost first; empty when it draws nothing.</summary>
    /// <param name="content">What the glow surrounds.</param>
    /// <param name="colour">The glow's colour, its alpha the glow's opacity.</param>
    /// <param name="radius">How far the halo reaches beyond the content.</param>
    /// <param name="twice">
    /// Whether it is applied twice, the outer glow wrapping the inner one and the content — what
    /// LibreOffice does to a custom shape, whose primitive wraps the geometry object's own.
    /// </param>
    /// <param name="visible">The area that is drawn at all — the page, or the printed part of it.</param>
    /// <param name="rasteriser">The sink that renders <see cref="EffectContent.Draw"/>, or null.</param>
    public static IReadOnlyList<EffectLayer> Glow(
        EffectContent content,
        Colour colour,
        Length radius,
        bool twice,
        DocRect visible,
        IDrawingSink? rasteriser = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (colour.A == 0) return [];

        // The geometry gives the mask exactly where it describes the content; only what it cannot
        // describe — text, a picture's own transparency — is rendered for it.
        Coverage? coverage = Coverage.Of(content, content.MaskFromPixels ? rasteriser : null);
        if (coverage is null) return [];

        double radiusUnits = ModelUnits(radius);
        if (radiusUnits <= 0) return [];

        Range view = Range.Of(visible);

        EffectLayer? inner = GlowLayer(coverage, null, colour, radiusUnits, view);
        if (inner is not { } first) return [];
        if (!twice) return [first];

        // The outer glow wraps the inner glow's bitmap and the content, so its content is both and
        // its range is the inner bitmap's grown once more.
        EffectLayer? outer = GlowLayer(coverage, first, colour, radiusUnits, view);
        return outer is { } second ? [second, first] : [first];
    }

    /// <summary>
    /// The bitmap softened content is drawn as in place of itself, or null when the effect cannot
    /// be computed here and the content should be drawn as it is.
    /// </summary>
    /// <param name="content">What is softened.</param>
    /// <param name="radius">How far in from the outline the content fades.</param>
    /// <param name="twice">Whether it is applied twice, for the reason <see cref="Glow"/> gives.</param>
    /// <param name="visible">The area that is drawn at all.</param>
    /// <param name="rasteriser">The sink that renders <see cref="EffectContent.Draw"/>, or null.</param>
    public static EffectLayer? SoftEdge(
        EffectContent content,
        Length radius,
        bool twice,
        DocRect visible,
        IDrawingSink? rasteriser = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Without the content's pixels only one colour of fill can be softened: its colour plane
        // is that colour and its alpha is its coverage.
        bool fromPixels = content.Draw is not null && rasteriser is not null
                          && (content.SolidColour is null || content.MaskFromPixels);
        if (!fromPixels && content.SolidColour is null) return null;

        Coverage? coverage = Coverage.Of(content, fromPixels ? rasteriser : null);
        if (coverage is null) return null;

        double radiusUnits = ModelUnits(radius);
        if (radiusUnits <= 0) return null;

        Range range = coverage.Range;
        if (range.Width <= 0 || range.Height <= 0) return null;

        Range clipped = range.Intersect(Range.Of(visible).Grow(radiusUnits * 2));
        if (clipped.IsEmpty) return null;

        Grid? grid = Grid.For(range, clipped);
        if (grid is not { } g) return null;
        if (Math.Ceiling(radiusUnits * DiscretePerUnit) < 1.0) return null;

        double discreteRadius = Math.Ceiling(radiusUnits * DiscretePerUnit) * g.Scale;

        // The content's own alpha is the base; the processed mask multiplies it.
        (byte[] alpha, byte[]? rgb) = coverage.Render(g);
        if (alpha.Length == 0) return null;

        alpha = Multiply(alpha, Process(alpha, g.Width, g.Height, -discreteRadius, discreteRadius, 0));
        if (twice) alpha = Multiply(alpha, Process(alpha, g.Width, g.Height, -discreteRadius, discreteRadius, 0));

        RasterImage image = rgb is null
            ? Image(content.SolidColour!.Value, alpha, g.Width, g.Height)
            : Image(rgb, alpha, g.Width, g.Height);

        return new EffectLayer(image, clipped.ToRect());
    }

    /// <summary>
    /// An effect bitmap with every colour replaced and its alpha kept — what a shadow casts of
    /// a glow (<c>BColorModifier_replace</c>, which a <c>ShadowPrimitive2D</c> wraps its content
    /// in, glow included: <c>sdrcustomshapeprimitive2d.cxx</c>:47-52 puts the glow inside the
    /// shadow for tdf#132199).
    /// </summary>
    public static RasterImage Recoloured(RasterImage image, Colour colour)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!image.IsDecoded) return image;

        ReadOnlySpan<byte> source = image.Pixels.Span;
        byte[] pixels = new byte[source.Length];
        for (int i = 0; i + 3 < source.Length; i += 4)
        {
            pixels[i] = colour.R;
            pixels[i + 1] = colour.G;
            pixels[i + 2] = colour.B;
            pixels[i + 3] = source[i + 3];
        }

        return new RasterImage { Width = image.Width, Height = image.Height, Pixels = pixels };
    }

    /// <summary>
    /// A radius as the model holds it: a <c>SdrMetricItem</c>, a whole number of hundredths of a
    /// millimetre (<c>convertEmuToHmm</c> rounds). It decides the ceiling the discrete radius is
    /// taken from — a 20 pt soft edge is 705.6 units stated, 706 held, and 200.0 against 200.1
    /// discrete pixels is a whole pixel of erosion.
    /// </summary>
    private static double ModelUnits(Length radius) => Math.Round(radius.Emu / EmuPerMm100, MidpointRounding.AwayFromZero);

    private static EffectLayer? GlowLayer(Coverage content, EffectLayer? inner, Colour colour, double radius, Range view)
    {
        Range range = inner is { } beneath ? Range.Of(beneath.Destination) : content.Range;
        range = range.Grow(radius);

        Range clipped = range.Intersect(view.Grow(radius));
        if (clipped.IsEmpty) return null;

        Grid? grid = Grid.For(range, clipped);
        if (grid is not { } g) return null;

        double discreteRadius = Math.Ceiling(radius * DiscretePerUnit) * g.Scale;
        if (Math.Ceiling(radius * DiscretePerUnit) < 1.0) return null;

        byte[] alpha = content.Render(g).Alpha;
        if (alpha.Length == 0) return null;
        if (inner is { } under) Underlay(alpha, g, under);

        // fDiscreteGlowRadius is the halo's whole width, and blurring a sharp edge reaches half
        // of it on each side, so both the dilation and the blur take half (glowprimitive2d.cxx).
        byte[] mask = Process(
            alpha, g.Width, g.Height, discreteRadius / 2.0, discreteRadius / 2.0,
            (byte)(255 - colour.A));

        return new EffectLayer(Image(colour, mask, g.Width, g.Height), clipped.ToRect());
    }

    /// <summary>Marks every pixel the inner glow's bitmap gives any alpha at all.</summary>
    private static void Underlay(byte[] alpha, Grid grid, EffectLayer inner)
    {
        RasterImage image = inner.Image;
        ReadOnlySpan<byte> pixels = image.Pixels.Span;
        DocRect at = inner.Destination;

        double left = at.X.Emu / EmuPerMm100;
        double top = at.Y.Emu / EmuPerMm100;
        double width = at.Width.Emu / EmuPerMm100;
        double height = at.Height.Emu / EmuPerMm100;
        if (width <= 0 || height <= 0) return;

        for (int y = 0; y < grid.Height; y++)
        {
            double objectY = grid.ObjectY(y + 0.5);
            int sy = (int)Math.Floor((objectY - top) / height * image.Height);
            if (sy < 0 || sy >= image.Height) continue;

            for (int x = 0; x < grid.Width; x++)
            {
                double objectX = grid.ObjectX(x + 0.5);
                int sx = (int)Math.Floor((objectX - left) / width * image.Width);
                if (sx < 0 || sx >= image.Width) continue;

                byte a = pixels[(sy * image.Width + sx) * 4 + 3];
                if (a > alpha[y * grid.Width + x]) alpha[y * grid.Width + x] = a;
            }
        }
    }

    private static byte[] ToAlpha(float[] coverage)
    {
        byte[] alpha = new byte[coverage.Length];
        for (int i = 0; i < coverage.Length; i++)
        {
            float c = coverage[i];
            alpha[i] = c <= 0f ? (byte)0 : (byte)Math.Clamp((int)Math.Round(c * 255f), 1, 255);
        }

        return alpha;
    }

    private static byte[] Multiply(byte[] a, byte[] b)
    {
        byte[] result = new byte[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = (byte)(a[i] * b[i] / 255);
        return result;
    }

    private static RasterImage Image(Colour colour, byte[] alpha, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < alpha.Length; i++)
        {
            pixels[i * 4] = colour.R;
            pixels[i * 4 + 1] = colour.G;
            pixels[i * 4 + 2] = colour.B;
            pixels[i * 4 + 3] = alpha[i];
        }

        return new RasterImage { Width = width, Height = height, Pixels = pixels };
    }

    private static RasterImage Image(byte[] rgb, byte[] alpha, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < alpha.Length; i++)
        {
            pixels[i * 4] = rgb[i * 3];
            pixels[i * 4 + 1] = rgb[i * 3 + 1];
            pixels[i * 4 + 2] = rgb[i * 3 + 2];
            pixels[i * 4 + 3] = alpha[i];
        }

        return new RasterImage { Width = width, Height = height, Pixels = pixels };
    }

    /// <summary>
    /// <c>ProcessAndBlurAlphaMask</c>: the mask a content alpha becomes, in the alpha domain.
    /// </summary>
    /// <param name="alpha">The content's alpha, 0 transparent.</param>
    /// <param name="width">Its width in pixels.</param>
    /// <param name="height">Its height in pixels.</param>
    /// <param name="erodeDilate">Positive dilates the inside, negative erodes it.</param>
    /// <param name="blur">The stack blur's radius before the clamp.</param>
    /// <param name="transparency">The transparency the inside is given before the blur.</param>
    internal static byte[] Process(
        byte[] alpha, int width, int height, double erodeDilate, double blur, byte transparency)
    {
        // In the transparency domain, 1-bit: only a fully transparent pixel is outside.
        byte[] mask = new byte[alpha.Length];
        for (int i = 0; i < alpha.Length; i++) mask[i] = alpha[i] == 0 ? (byte)255 : (byte)0;

        int w = width;
        int h = height;
        double scale = 1.0;
        while (blur > 254 || h > 1000 || w > 1000)
        {
            scale /= 2;
            blur /= 2;
            erodeDilate /= 2;
            w /= 2;
            h /= 2;
        }

        if (scale != 1.0)
        {
            w = Math.Max(1, (int)Math.Round(width * scale));
            h = Math.Max(1, (int)Math.Round(height * scale));
            mask = Nearest(mask, width, height, w, h);
        }

        if (erodeDilate > 0)
            mask = Morphology(mask, w, h, (int)erodeDilate, dilate: true, outside: 255);
        else if (erodeDilate < 0)
            mask = Morphology(mask, w, h, (int)-erodeDilate, dilate: false, outside: 255);

        if (transparency != 0)
        {
            for (int i = 0; i < mask.Length; i++)
                if (mask[i] == 0) mask[i] = transparency;
        }

        StackBlur(mask, w, h, Math.Clamp((int)blur, 2, 254));

        if (w != width || h != height) mask = Bilinear(mask, w, h, width, height);

        for (int i = 0; i < mask.Length; i++) mask[i] = (byte)(255 - mask[i]);
        return mask;
    }

    /// <summary>
    /// A separable square erosion or dilation (<c>BitmapBasicMorphologyFilter.cxx</c>): black is
    /// the inside, so a dilation takes the minimum over the window and an erosion the maximum.
    /// </summary>
    internal static byte[] Morphology(byte[] source, int width, int height, int radius, bool dilate, byte outside)
    {
        if (radius <= 0) return source;

        byte neutral = dilate ? (byte)255 : (byte)0;
        byte edge = dilate ? neutral : outside;

        byte[] horizontal = new byte[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int from = x - radius;
                int to = x + radius;
                byte value = from < 0 || to > width - 1 ? edge : neutral;
                from = Math.Max(from, 0);
                to = Math.Min(to, width - 1);

                for (int i = from; i <= to; i++)
                {
                    byte v = source[y * width + i];
                    value = dilate ? Math.Min(value, v) : Math.Max(value, v);
                }

                horizontal[y * width + x] = value;
            }
        }

        byte[] result = new byte[source.Length];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int from = y - radius;
                int to = y + radius;
                byte value = from < 0 || to > height - 1 ? edge : neutral;
                from = Math.Max(from, 0);
                to = Math.Min(to, height - 1);

                for (int i = from; i <= to; i++)
                {
                    byte v = horizontal[i * width + x];
                    value = dilate ? Math.Min(value, v) : Math.Max(value, v);
                }

                result[y * width + x] = value;
            }
        }

        return result;
    }

    private static readonly short[] MultiplyTable =
    [
        512, 512, 456, 512, 328, 456, 335, 512, 405, 328, 271, 456, 388, 335, 292, 512, 454,
        405, 364, 328, 298, 271, 496, 456, 420, 388, 360, 335, 312, 292, 273, 512, 482, 454,
        428, 405, 383, 364, 345, 328, 312, 298, 284, 271, 259, 496, 475, 456, 437, 420, 404,
        388, 374, 360, 347, 335, 323, 312, 302, 292, 282, 273, 265, 512, 497, 482, 468, 454,
        441, 428, 417, 405, 394, 383, 373, 364, 354, 345, 337, 328, 320, 312, 305, 298, 291,
        284, 278, 271, 265, 259, 507, 496, 485, 475, 465, 456, 446, 437, 428, 420, 412, 404,
        396, 388, 381, 374, 367, 360, 354, 347, 341, 335, 329, 323, 318, 312, 307, 302, 297,
        292, 287, 282, 278, 273, 269, 265, 261, 512, 505, 497, 489, 482, 475, 468, 461, 454,
        447, 441, 435, 428, 422, 417, 411, 405, 399, 394, 389, 383, 378, 373, 368, 364, 359,
        354, 350, 345, 341, 337, 332, 328, 324, 320, 316, 312, 309, 305, 301, 298, 294, 291,
        287, 284, 281, 278, 274, 271, 268, 265, 262, 259, 257, 507, 501, 496, 491, 485, 480,
        475, 470, 465, 460, 456, 451, 446, 442, 437, 433, 428, 424, 420, 416, 412, 408, 404,
        400, 396, 392, 388, 385, 381, 377, 374, 370, 367, 363, 360, 357, 354, 350, 347, 344,
        341, 338, 335, 332, 329, 326, 323, 320, 318, 315, 312, 310, 307, 304, 302, 299, 297,
        294, 292, 289, 287, 285, 282, 280, 278, 275, 273, 271, 269, 267, 265, 263, 261, 259,
    ];

    private static readonly short[] ShiftTable =
    [
        9, 11, 12, 13, 13, 14, 14, 15, 15, 15, 15, 16, 16, 16, 16, 17, 17, 17, 17, 17, 17, 17,
        18, 18, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19,
        19, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 21, 21, 21,
        21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
        21, 21, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22,
        22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23,
        23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23,
        23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23,
        23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
    ];

    /// <summary>
    /// The 8-bit stack blur of <c>vcl/source/bitmap/BitmapFilterStackBlur.cxx</c>, horizontal pass
    /// then vertical, in place, with its multiply-and-shift division.
    /// </summary>
    internal static void StackBlur(byte[] pixels, int width, int height, int radius)
    {
        Pass(pixels, width, height, radius, horizontal: true);
        Pass(pixels, width, height, radius, horizontal: false);
    }

    private static void Pass(byte[] pixels, int width, int height, int radius, bool horizontal)
    {
        int div = radius + radius + 1;
        int length = horizontal ? width : height;
        int outerCount = horizontal ? height : width;
        int lastIndex = length - 1;
        int multiply = MultiplyTable[radius];
        int shift = ShiftTable[radius];

        byte[] stack = new byte[div];
        int[] positions = new int[div];
        int[] weights = new int[div];
        for (int i = 0; i < div; i++)
        {
            positions[i] = Math.Clamp(i - radius, 0, lastIndex);
            weights[i] = radius + 1 - Math.Abs(i - radius);
        }

        int Index(int outer, int inner) => horizontal ? outer * width + inner : inner * width + outer;

        for (int outer = 0; outer < outerCount; outer++)
        {
            int sum = 0;
            int inSum = 0;
            int outSum = 0;

            for (int i = 0; i < div; i++)
            {
                byte source = pixels[Index(outer, positions[i])];
                stack[i] = source;
                sum += source * weights[i];
                if (i - radius > 0) inSum += source;
                else outSum += source;
            }

            int stackIndex = radius;
            int position = Math.Min(radius, lastIndex);
            byte current = pixels[Index(outer, position)];

            for (int inner = 0; inner < length; inner++)
            {
                pixels[Index(outer, inner)] = (byte)((multiply * sum) >> shift);
                sum -= outSum;

                int start = stackIndex + div - radius;
                if (start >= div) start -= div;
                outSum -= stack[start];

                if (position < lastIndex)
                {
                    position++;
                    current = pixels[Index(outer, position)];
                }

                stack[start] = current;
                inSum += current;
                sum += inSum;

                stackIndex++;
                if (stackIndex >= div) stackIndex = 0;

                outSum += stack[stackIndex];
                inSum -= stack[stackIndex];
            }
        }
    }

    private static byte[] Nearest(byte[] source, int width, int height, int newWidth, int newHeight)
    {
        byte[] result = new byte[newWidth * newHeight];
        for (int y = 0; y < newHeight; y++)
        {
            int sy = Math.Min(height - 1, (int)((y + 0.5) * height / newHeight));
            for (int x = 0; x < newWidth; x++)
            {
                int sx = Math.Min(width - 1, (int)((x + 0.5) * width / newWidth));
                result[y * newWidth + x] = source[sy * width + sx];
            }
        }

        return result;
    }

    private static byte[] Bilinear(byte[] source, int width, int height, int newWidth, int newHeight)
    {
        byte[] result = new byte[newWidth * newHeight];
        for (int y = 0; y < newHeight; y++)
        {
            double fy = Math.Clamp(((y + 0.5) * height / newHeight) - 0.5, 0, height - 1);
            int y0 = (int)fy;
            int y1 = Math.Min(y0 + 1, height - 1);
            double ty = fy - y0;

            for (int x = 0; x < newWidth; x++)
            {
                double fx = Math.Clamp(((x + 0.5) * width / newWidth) - 0.5, 0, width - 1);
                int x0 = (int)fx;
                int x1 = Math.Min(x0 + 1, width - 1);
                double tx = fx - x0;

                double top = source[y0 * width + x0] * (1 - tx) + source[y0 * width + x1] * tx;
                double bottom = source[y1 * width + x0] * (1 - tx) + source[y1 * width + x1] * tx;
                result[y * newWidth + x] = (byte)Math.Round(top * (1 - ty) + bottom * ty);
            }
        }

        return result;
    }

    /// <summary>A rectangle in hundredths of a millimetre, the view's discrete unit.</summary>
    private readonly record struct Range(double X, double Y, double Width, double Height)
    {
        public bool IsEmpty => Width <= 0 || Height <= 0;

        public Range Grow(double amount)
            => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);

        public Range Intersect(Range other)
        {
            double left = Math.Max(X, other.X);
            double top = Math.Max(Y, other.Y);
            double right = Math.Min(X + Width, other.X + other.Width);
            double bottom = Math.Min(Y + Height, other.Y + other.Height);
            return new Range(left, top, right - left, bottom - top);
        }

        public DocRect ToRect()
            => new(
                Length.FromEmu((long)Math.Round(X * EmuPerMm100)),
                Length.FromEmu((long)Math.Round(Y * EmuPerMm100)),
                Length.FromEmu((long)Math.Round(Width * EmuPerMm100)),
                Length.FromEmu((long)Math.Round(Height * EmuPerMm100)));

        public static Range Of(DocRect rect)
            => new(
                rect.X.Emu / EmuPerMm100, rect.Y.Emu / EmuPerMm100,
                rect.Width.Emu / EmuPerMm100, rect.Height.Emu / EmuPerMm100);
    }

    /// <summary>The pixel grid a range is rendered into, after the 250 000-pixel reduction.</summary>
    private readonly record struct Grid(
        int Width, int Height, double Scale, double OriginX, double OriginY, double PixelsPerUnitX,
        double PixelsPerUnitY)
    {
        public static Grid? For(Range full, Range clipped)
        {
            double discreteWidth = Math.Ceiling(full.Width * DiscretePerUnit);
            double discreteHeight = Math.Ceiling(full.Height * DiscretePerUnit);
            if (discreteWidth < 2.0 || discreteHeight < 2.0) return null;

            double clippedWidth = Math.Ceiling(clipped.Width * DiscretePerUnit);
            double clippedHeight = Math.Ceiling(clipped.Height * DiscretePerUnit);

            double reduce = 1.0;
            int width = (int)clippedWidth;
            int height = (int)clippedHeight;
            double area = clippedWidth * clippedHeight;
            if (area > MaximumQuadraticPixels)
            {
                reduce = Math.Sqrt(MaximumQuadraticPixels / area);
                width = (int)Math.Round(clippedWidth * reduce, MidpointRounding.AwayFromZero);
                height = (int)Math.Round(clippedHeight * reduce, MidpointRounding.AwayFromZero);
            }

            if (width <= 0 || height <= 0) return null;

            double scale = ((width / clippedWidth) + (height / clippedHeight)) * 0.5;

            return new Grid(
                width, height, scale, clipped.X, clipped.Y,
                discreteWidth / full.Width * reduce, discreteHeight / full.Height * reduce);
        }

        public double PixelX(double objectX) => (objectX - OriginX) * PixelsPerUnitX;

        public double PixelY(double objectY) => (objectY - OriginY) * PixelsPerUnitY;

        public double ObjectX(double pixelX) => OriginX + pixelX / PixelsPerUnitX;

        public double ObjectY(double pixelY) => OriginY + pixelY / PixelsPerUnitY;
    }

    /// <summary>
    /// The content's coverage of a grid: from its geometry, or from its pixels when a backend
    /// renders them.
    /// </summary>
    private sealed class Coverage
    {
        private readonly List<List<(double X, double Y)>> _fill = [];
        private readonly List<List<(double X, double Y)>> _stroke = [];
        private double _halfWidth;
        private Action<IDrawingSink>? _draw;
        private IDrawingSink? _rasteriser;

        public Range Range { get; private set; }

        /// <summary>The coverage of some content, or null when it has no extent.</summary>
        /// <param name="content">The content.</param>
        /// <param name="rasteriser">The sink to take its pixels from, or null for its geometry.</param>
        public static Coverage? Of(EffectContent content, IDrawingSink? rasteriser)
        {
            Coverage coverage = new();
            if (rasteriser is not null && content.Draw is not null)
            {
                coverage._draw = content.Draw;
                coverage._rasteriser = rasteriser;
            }

            foreach (GraphicsPath path in content.Fill) coverage._fill.AddRange(Flatten(path));

            if (content.Stroke.Count > 0)
            {
                foreach (GraphicsPath path in content.Stroke) coverage._stroke.AddRange(Flatten(path));

                // A hairline is one device pixel; one hundredth of a millimetre is close enough.
                coverage._halfWidth = Math.Max(content.StrokeWidth.Emu / EmuPerMm100, 1.0) / 2.0;
            }

            if (content.Bounds is { } bounds)
            {
                if (bounds.Width <= Length.Zero || bounds.Height <= Length.Zero) return null;
                coverage.Range = Range.Of(bounds);
                return coverage;
            }

            coverage.SnapToModel();

            double left = double.MaxValue, top = double.MaxValue;
            double right = double.MinValue, bottom = double.MinValue;

            foreach (List<(double X, double Y)> polygon in coverage._fill)
            {
                foreach ((double x, double y) in polygon)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            }

            foreach (List<(double X, double Y)> polygon in coverage._stroke)
            {
                foreach ((double x, double y) in polygon)
                {
                    left = Math.Min(left, x - coverage._halfWidth);
                    right = Math.Max(right, x + coverage._halfWidth);
                    top = Math.Min(top, y - coverage._halfWidth);
                    bottom = Math.Max(bottom, y + coverage._halfWidth);
                }
            }

            if (left >= right || top >= bottom) return null;

            coverage.Range = new Range(left, top, right - left, bottom - top);
            return coverage;
        }

        /// <summary>
        /// Moves the geometry onto the rectangle LibreOffice's model holds for it: whole hundredths
        /// of a millimetre, one short of the stated size.
        /// </summary>
        /// <remarks>
        /// An <c>SdrObject</c>'s logic rectangle is a <c>tools::Rectangle</c> of integers, and the
        /// shape the effect reads is that rectangle's: the reference's own <c>--convert-to odp</c>
        /// of a 2 000 000 × 1 200 000 EMU rectangle (5555.6 × 3333.3 hundredths) states
        /// <c>5.555cm × 3.332cm</c>, and a 127 000 EMU square (352.8) is drawn 352 wide — the
        /// rounded size less one in both axes, at the rounded position. It is a thirtieth of a point,
        /// and it matters because a pixel with any coverage at all counts as inside: drawn at the
        /// stated size, the softened rectangle's last column was inside and the whole ramp sat a
        /// pixel out, a mean mask difference of 5.1 levels where the model's size gives 0.0.
        /// </remarks>
        private void SnapToModel()
        {
            double left = double.MaxValue, top = double.MaxValue;
            double right = double.MinValue, bottom = double.MinValue;
            foreach (List<(double X, double Y)> polygon in _fill.Concat(_stroke))
            {
                foreach ((double x, double y) in polygon)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            }

            if (right - left < 2 || bottom - top < 2) return;

            double toLeft = Math.Round(left, MidpointRounding.AwayFromZero);
            double toTop = Math.Round(top, MidpointRounding.AwayFromZero);
            double scaleX = (Math.Round(right - left, MidpointRounding.AwayFromZero) - 1) / (right - left);
            double scaleY = (Math.Round(bottom - top, MidpointRounding.AwayFromZero) - 1) / (bottom - top);

            foreach (List<(double X, double Y)> polygon in _fill.Concat(_stroke))
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    polygon[i] = (
                        toLeft + (polygon[i].X - left) * scaleX,
                        toTop + (polygon[i].Y - top) * scaleY);
                }
            }

            // The rendered content goes the same way, as a transform round its drawing.
            if (_draw is { } draw)
            {
                AffineTransform onto = new(
                    scaleX, 0, 0, scaleY,
                    (toLeft - left * scaleX) * EmuPerMm100,
                    (toTop - top * scaleY) * EmuPerMm100);

                _draw = target =>
                {
                    target.Save();
                    try
                    {
                        target.Transform(onto);
                        draw(target);
                    }
                    finally
                    {
                        target.Restore();
                    }
                };
            }
        }

        /// <summary>
        /// The content's alpha on a grid, and its colour plane (RGB, three bytes a pixel) when it
        /// was rendered; an empty alpha when the backend could not render it.
        /// </summary>
        public (byte[] Alpha, byte[]? Rgb) Render(Grid grid)
        {
            if (_draw is null || _rasteriser is null) return (ToAlpha(Geometry(grid)), null);

            DocRect area = new Range(
                grid.OriginX, grid.OriginY,
                grid.Width / grid.PixelsPerUnitX, grid.Height / grid.PixelsPerUnitY).ToRect();

            RasterImage? image = _rasteriser.Rasterise(grid.Width, grid.Height, area, _draw);
            if (image is not { IsDecoded: true } || image.Width != grid.Width || image.Height != grid.Height)
                return ([], null);

            ReadOnlySpan<byte> pixels = image.Pixels.Span;
            byte[] alpha = new byte[grid.Width * grid.Height];
            byte[] rgb = new byte[alpha.Length * 3];
            for (int i = 0; i < alpha.Length; i++)
            {
                rgb[i * 3] = pixels[i * 4];
                rgb[i * 3 + 1] = pixels[i * 4 + 1];
                rgb[i * 3 + 2] = pixels[i * 4 + 2];
                alpha[i] = pixels[i * 4 + 3];
            }

            return (alpha, rgb);
        }

        /// <summary>
        /// The fraction of each pixel the content covers: four sub-scanlines per row, each
        /// covered exactly along its length under the non-zero rule, and the stroke by distance.
        /// </summary>
        private float[] Geometry(Grid grid)
        {
            float[] coverage = new float[grid.Width * grid.Height];
            const int Samples = 4;
            List<(double X, int Direction)> crossings = [];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int sub = 0; sub < Samples; sub++)
                {
                    double py = y + (sub + 0.5) / Samples;
                    crossings.Clear();

                    foreach (List<(double X, double Y)> polygon in _fill)
                    {
                        for (int i = 0; i < polygon.Count; i++)
                        {
                            (double ax, double ay) = polygon[i];
                            (double bx, double by) = polygon[(i + 1) % polygon.Count];
                            double y0 = grid.PixelY(ay);
                            double y1 = grid.PixelY(by);
                            if (y0 == y1) continue;
                            if (py < Math.Min(y0, y1) || py >= Math.Max(y0, y1)) continue;

                            double t = (py - y0) / (y1 - y0);
                            double x = grid.PixelX(ax + (bx - ax) * t);
                            crossings.Add((x, y1 > y0 ? 1 : -1));
                        }
                    }

                    if (crossings.Count < 2) continue;
                    crossings.Sort((a, b) => a.X.CompareTo(b.X));

                    int winding = 0;
                    for (int i = 0; i < crossings.Count - 1; i++)
                    {
                        winding += crossings[i].Direction;
                        if (winding == 0) continue;

                        Span(coverage, grid.Width, y, crossings[i].X, crossings[i + 1].X, 1f / Samples);
                    }
                }
            }

            if (_stroke.Count > 0) Stroke(coverage, grid);

            for (int i = 0; i < coverage.Length; i++) coverage[i] = Math.Min(coverage[i], 1f);
            return coverage;
        }

        private static void Span(float[] coverage, int width, int row, double from, double to, float weight)
        {
            from = Math.Clamp(from, 0, width);
            to = Math.Clamp(to, 0, width);
            if (to <= from) return;

            int first = (int)Math.Floor(from);
            int last = Math.Min((int)Math.Floor(to), width - 1);
            for (int x = first; x <= last; x++)
            {
                double covered = Math.Min(to, x + 1) - Math.Max(from, x);
                if (covered > 0) coverage[row * width + x] += (float)covered * weight;
            }
        }

        private void Stroke(float[] coverage, Grid grid)
        {
            double halfX = _halfWidth * grid.PixelsPerUnitX;
            double halfY = _halfWidth * grid.PixelsPerUnitY;
            double half = (halfX + halfY) / 2;

            foreach (List<(double X, double Y)> polyline in _stroke)
            {
                for (int i = 0; i + 1 < polyline.Count; i++)
                {
                    double ax = grid.PixelX(polyline[i].X), ay = grid.PixelY(polyline[i].Y);
                    double bx = grid.PixelX(polyline[i + 1].X), by = grid.PixelY(polyline[i + 1].Y);

                    int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - half - 1));
                    int x1 = Math.Min(grid.Width - 1, (int)Math.Ceiling(Math.Max(ax, bx) + half + 1));
                    int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - half - 1));
                    int y1 = Math.Min(grid.Height - 1, (int)Math.Ceiling(Math.Max(ay, by) + half + 1));

                    double dx = bx - ax, dy = by - ay;
                    double lengthSquared = dx * dx + dy * dy;

                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            double cx = x + 0.5, cy = y + 0.5;
                            double t = lengthSquared > 0
                                ? Math.Clamp(((cx - ax) * dx + (cy - ay) * dy) / lengthSquared, 0, 1)
                                : 0;
                            double ex = ax + dx * t - cx, ey = ay + dy * t - cy;
                            double distance = Math.Sqrt(ex * ex + ey * ey);
                            double covered = Math.Clamp(half + 0.5 - distance, 0, 1);
                            if (covered > 0)
                            {
                                int index = y * grid.Width + x;
                                coverage[index] = Math.Max(coverage[index], (float)covered);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Each subpath as a polygon in hundredths of a millimetre, curves flattened.</summary>
        private static List<List<(double X, double Y)>> Flatten(GraphicsPath path)
        {
            List<List<(double X, double Y)>> result = [];
            List<(double X, double Y)>? current = null;
            (double X, double Y) last = (0, 0);
            (double X, double Y) start = (0, 0);

            static (double X, double Y) P(DocPoint p) => (p.X.Emu / EmuPerMm100, p.Y.Emu / EmuPerMm100);

            foreach (PathCommand command in path.Commands)
            {
                switch (command.Verb)
                {
                    case PathVerb.MoveTo:
                        current = [];
                        result.Add(current);
                        last = start = P(command.Point);
                        current.Add(last);
                        break;
                    case PathVerb.LineTo:
                        if (current is null) { current = []; result.Add(current); current.Add(last); }
                        last = P(command.Point);
                        current.Add(last);
                        break;
                    case PathVerb.CubicTo:
                        if (current is null) { current = []; result.Add(current); current.Add(last); }
                        (double X, double Y) c1 = P(command.Control1);
                        (double X, double Y) c2 = P(command.Control2);
                        (double X, double Y) end = P(command.Point);
                        const int Steps = 16;
                        for (int i = 1; i <= Steps; i++)
                        {
                            double t = (double)i / Steps, u = 1 - t;
                            current.Add((
                                u * u * u * last.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                                u * u * u * last.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y));
                        }

                        last = end;
                        break;
                    case PathVerb.Close:
                        if (current is not null && current.Count > 0) current.Add(start);
                        last = start;
                        current = null;
                        break;
                }
            }

            result.RemoveAll(polygon => polygon.Count < 2);
            return result;
        }
    }
}
