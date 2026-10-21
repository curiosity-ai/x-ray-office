using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;

namespace XRay.Office.Presentations.Layout;

/// <summary>
/// A shape's glow — DrawingML's <c>a:glow</c>.
/// </summary>
/// <param name="Colour">The glow's colour, its alpha the glow's opacity.</param>
/// <param name="Radius">How far the halo reaches beyond the shape.</param>
/// <param name="Twice">
/// Whether LibreOffice applies it twice. A DrawingML custom shape — every <c>p:sp</c> that is not a
/// presentation object — carries the effect on the custom shape <em>and</em> on the geometry
/// object the shape engine renders it from (<c>SdrCustomShapePrimitive2D</c> wraps its
/// sub-primitives, which are the render object's own and already carry one), so 26.8.1.1 draws a
/// glow of the glow: two bitmaps, the outer reaching twice the radius. A picture or a text frame
/// takes it once. Measured on <c>slide-glow-soft-edge.pptx</c>: an 18 pt glow on a rectangle is
/// two images reaching 36 and 18 pt.
/// </param>
public readonly record struct SlideGlow(Colour Colour, Length Radius, bool Twice);

/// <summary>
/// A shape's soft edge — DrawingML's <c>a:softEdge</c>.
/// </summary>
/// <param name="Radius">How far in from the outline the shape fades.</param>
/// <param name="Twice">Whether it is applied twice, for the reason <see cref="SlideGlow.Twice"/> gives.</param>
public readonly record struct SlideSoftEdge(Length Radius, bool Twice);

/// <summary>
/// Glow and soft edge, rasterised the way LibreOffice 26.8.1.1 rasterises them.
/// </summary>
/// <remarks>
/// <para>
/// Both are bitmaps in the reference's PDF, never vector: <c>GlowPrimitive2D</c> and
/// <c>SoftEdgePrimitive2D</c> (<c>drawinglayer/source/primitive2d/glowprimitive2d.cxx</c>,
/// <c>softedgeprimitive2d.cxx</c> at tag 26.8.1.1) render their content to an alpha mask, process
/// it and decompose to a <c>BitmapPrimitive2D</c>, which the PDF export writes as an image with an
/// <c>/SMask</c>. The shape's text is not part of either — a soft edge is applied before the text
/// is added and the glow to the geometry alone (<c>sdrcustomshapeprimitive2d.cxx</c>:39-52) — so
/// it stays real text.
/// </para>
/// <para>
/// The steps are the reference's, in its order:
/// </para>
/// <list type="number">
///   <item><description>The content's range, grown by the glow's radius (a soft edge's is not
///   grown), clipped to the page grown by the radius (twice the radius for a soft
///   edge).</description></item>
///   <item><description>Its size in the view's discrete units, which for the PDF export are
///   hundredths of a millimetre, reduced to at most 250 000 pixels by a uniform factor
///   (<c>implPrepareConversion</c>, <c>drawinglayer/source/tools/converters.cxx</c>:44-70) —
///   every bitmap the probe holds is within a few hundred pixels of that
///   area.</description></item>
///   <item><description>The content rendered into that grid as an alpha mask, of which only
///   the fully transparent pixels count as outside (<c>CreateMask(COL_WHITE)</c>).</description></item>
///   <item><description><c>ProcessAndBlurAlphaMask</c>
///   (<c>GlowSoftEgdeShadowTools.cxx</c>): halve while the blur exceeds 254 pixels or a side
///   exceeds 1000, dilate (glow, by half the radius) or erode (soft edge, by the radius, the
///   outside counting as transparent) with a square structuring element, give the inside the
///   glow's transparency, stack-blur (half the radius for a glow, the radius for a soft edge),
///   and scale back.</description></item>
///   <item><description>A glow is its colour under that mask; a soft edge is the content under
///   the product of its own alpha and the mask (<c>AlphaMask::BlendWith</c>).</description></item>
/// </list>
/// <para>
/// <strong>What is not reproduced</strong>: a soft edge on anything but a solid fill with no
/// outline — a picture, a gradient, a pattern — because rendering that content needs pixels this
/// layer cannot produce; such a shape is drawn as it always was. And the reference's own
/// rasteriser's anti-aliasing and its JPEG encoding of the colour plane, so the edges agree to a
/// pixel or two of the bitmap rather than exactly.
/// </para>
/// </remarks>
internal static class SlideEffects
{
    private const int MaximumQuadraticPixels = 250_000;

    private const double EmuPerMm100 = 360.0;

    /// <summary>One bitmap and where it goes, in slide coordinates.</summary>
    internal readonly record struct Layer(RasterImage Image, DocRect Destination);

    /// <summary>The bitmaps a glow is drawn as, outermost first; empty when it draws nothing.</summary>
    /// <param name="shape">The shape the glow belongs to.</param>
    /// <param name="glow">The glow.</param>
    /// <param name="page">The slide's size, which the bitmap is clipped against.</param>
    public static IReadOnlyList<Layer> Glow(PlacedShape shape, SlideGlow glow, DocSize page)
    {
        Content? content = Content.Of(shape, includeLine: true);
        if (content is null || glow.Colour.A == 0) return [];

        double radius = glow.Radius.Emu / EmuPerMm100;
        if (radius <= 0) return [];

        Layer? inner = GlowLayer(content, null, glow, radius, page);
        if (inner is not { } first) return [];
        if (!glow.Twice) return [first];

        // The custom shape's own glow wraps the render object's glow and its geometry, so its
        // content is both, and its range is the inner bitmap's grown once more.
        Layer? outer = GlowLayer(content, first, glow, radius, page);
        return outer is { } second ? [second, first] : [first];
    }

    /// <summary>
    /// The bitmap a softened shape is drawn as in place of its fill, or null when the effect
    /// does not apply to it here.
    /// </summary>
    public static Layer? SoftEdge(PlacedShape shape, SlideSoftEdge softEdge, DocSize page)
    {
        if (shape.Fill is not SolidPaint { Colour: var colour } || shape.Picture is not null) return null;
        if (shape.Line is not null) return null;

        Content? content = Content.Of(shape, includeLine: false);
        if (content is null) return null;

        double radius = softEdge.Radius.Emu / EmuPerMm100;
        if (radius <= 0) return null;

        Range range = content.Range;
        if (range.Width <= 0 || range.Height <= 0) return null;

        Range clipped = range.Intersect(PageRange(page).Grow(radius * 2));
        if (clipped.IsEmpty) return null;

        Grid? grid = Grid.For(range, clipped);
        if (grid is not { } g) return null;
        if (Math.Ceiling(radius) < 1.0) return null;

        double discreteRadius = Math.Ceiling(radius) * g.Scale;

        // The content's own coverage is its alpha; the processed mask multiplies it.
        float[] coverage = content.Coverage(g);
        byte[] alpha = ToAlpha(coverage);
        alpha = Multiply(alpha, Process(alpha, g.Width, g.Height, -discreteRadius, discreteRadius, 0));

        if (softEdge.Twice)
        {
            alpha = Multiply(alpha, Process(alpha, g.Width, g.Height, -discreteRadius, discreteRadius, 0));
        }

        return new Layer(Image(colour, alpha, g.Width, g.Height), clipped.ToRect());
    }

    private static Layer? GlowLayer(Content content, Layer? inner, SlideGlow glow, double radius, DocSize page)
    {
        Range range = inner is { } beneath ? Range.Of(beneath.Destination) : content.Range;
        range = range.Grow(radius);

        Range clipped = range.Intersect(PageRange(page).Grow(radius));
        if (clipped.IsEmpty) return null;

        Grid? grid = Grid.For(range, clipped);
        if (grid is not { } g) return null;

        double discreteRadius = Math.Ceiling(radius) * g.Scale;
        if (Math.Ceiling(radius) < 1.0) return null;

        float[] coverage = content.Coverage(g);
        byte[] alpha = ToAlpha(coverage);
        if (inner is { } under) Underlay(alpha, g, under);

        // fDiscreteGlowRadius is the halo's whole width, and blurring a sharp edge reaches half
        // of it on each side, so both the dilation and the blur take half (glowprimitive2d.cxx).
        byte[] mask = Process(
            alpha, g.Width, g.Height, discreteRadius / 2.0, discreteRadius / 2.0,
            (byte)(255 - glow.Colour.A));

        return new Layer(Image(glow.Colour, mask, g.Width, g.Height), clipped.ToRect());
    }

    /// <summary>Marks every pixel the inner glow's bitmap gives any alpha at all.</summary>
    private static void Underlay(byte[] alpha, Grid grid, Layer inner)
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

    private static Range PageRange(DocSize page)
        => new(0, 0, page.Width.Emu / EmuPerMm100, page.Height.Emu / EmuPerMm100);

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
            double discreteWidth = Math.Ceiling(full.Width);
            double discreteHeight = Math.Ceiling(full.Height);
            if (discreteWidth < 2.0 || discreteHeight < 2.0) return null;

            double clippedWidth = Math.Ceiling(clipped.Width);
            double clippedHeight = Math.Ceiling(clipped.Height);

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

    /// <summary>What a shape's effect is computed from: its fill area and its outline's stroke.</summary>
    private sealed class Content
    {
        private readonly List<List<(double X, double Y)>> _fill = [];
        private readonly List<List<(double X, double Y)>> _stroke = [];
        private double _halfWidth;

        public Range Range { get; private set; }

        public static Content? Of(PlacedShape shape, bool includeLine)
        {
            Content content = new();

            if (shape.Fill is not null || shape.Picture is not null)
                content._fill.AddRange(Flatten(shape.FillOutline));

            if (includeLine && shape.Line is { } line)
            {
                content._stroke.AddRange(Flatten(shape.StrokeOutline));

                // A hairline is one device pixel; one hundredth of a millimetre is close enough.
                content._halfWidth = Math.Max(line.Width.Emu / EmuPerMm100, 1.0) / 2.0;
            }

            double left = double.MaxValue, top = double.MaxValue;
            double right = double.MinValue, bottom = double.MinValue;

            foreach (List<(double X, double Y)> polygon in content._fill)
            {
                foreach ((double x, double y) in polygon)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            }

            foreach (List<(double X, double Y)> polygon in content._stroke)
            {
                foreach ((double x, double y) in polygon)
                {
                    left = Math.Min(left, x - content._halfWidth);
                    right = Math.Max(right, x + content._halfWidth);
                    top = Math.Min(top, y - content._halfWidth);
                    bottom = Math.Max(bottom, y + content._halfWidth);
                }
            }

            if (left >= right || top >= bottom) return null;

            content.Range = new Range(left, top, right - left, bottom - top);
            return content;
        }

        /// <summary>
        /// The fraction of each pixel the content covers: four sub-scanlines per row, each
        /// covered exactly along its length under the non-zero rule, and the stroke by distance.
        /// </summary>
        public float[] Coverage(Grid grid)
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
