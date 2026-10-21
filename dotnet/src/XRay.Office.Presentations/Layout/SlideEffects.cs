using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;

namespace XRay.Office.Presentations.Layout;

/// <summary>
/// A shape's glow — DrawingML's <c>a:glow</c>, ODF's <c>loext:glow-radius</c>.
/// </summary>
/// <param name="Colour">The glow's colour, its alpha the glow's opacity.</param>
/// <param name="Radius">How far the halo reaches beyond the shape.</param>
/// <param name="Twice">
/// Whether LibreOffice applies it twice. A rectangle custom shape — a DrawingML <c>p:sp</c> of
/// preset <c>rect</c> that is not a presentation object, an ODF <c>draw:custom-shape</c> of type
/// <c>rectangle</c> — carries the effect on the custom shape <em>and</em> on the
/// <c>SdrRectObj</c> its engine renders it from (<c>SdrCustomShapePrimitive2D</c> wraps its
/// sub-primitives, which are that object's own and already carry one), so 26.8 draws a glow of the
/// glow: two bitmaps, the outer reaching twice the radius. Any other geometry is rendered from an
/// <c>SdrPathObj</c>, which draws no effect, and a picture or a text frame has no inner object, so
/// they take it once. Measured on <c>slide-glow-soft-edge.pptx</c>: an 18 pt glow on a rectangle
/// is two images reaching 36 and 18 pt.
/// </param>
public readonly record struct SlideGlow(Colour Colour, Length Radius, bool Twice);

/// <summary>
/// A shape's soft edge — DrawingML's <c>a:softEdge</c>, ODF's <c>loext:softedge-radius</c>.
/// </summary>
/// <param name="Radius">How far in from the outline the shape fades.</param>
/// <param name="Twice">Whether it is applied twice, for the reason <see cref="SlideGlow.Twice"/> gives.</param>
public readonly record struct SlideSoftEdge(Length Radius, bool Twice);

/// <summary>
/// The glow round a shape's text — DrawingML's <c>a:rPr/a:effectLst/a:glow</c>, which LibreOffice
/// takes from the first paragraph or run stating one and applies to the whole body.
/// </summary>
/// <param name="Colour">The glow's colour, its alpha the glow's opacity.</param>
/// <param name="Radius">The radius as the file states it, before it is scaled for the text's size.</param>
/// <param name="ShapeFontSize">
/// The font height of the shape's own item set, which the scaling counts with the runs' — the
/// largest of them decides. A DrawingML shape inherits Impress's default drawing style, 18 pt:
/// measured on 12 pt text with a 5 pt and a 10 pt glow, the reference's bitmaps reach 1.32 and
/// 2.62 pt beyond the ink, the radii 18 pt gives (47 and 93 hundredths of a millimetre) and not
/// the 37 and 74 that 12 pt would.
/// </param>
public readonly record struct SlideTextGlow(Colour Colour, Length Radius, Length ShapeFontSize);

/// <summary>
/// Glow and soft edge on a slide's shapes: what each effect is computed from, handed to
/// <see cref="ShapeEffects"/>, which does the rasterising.
/// </summary>
internal static class SlideEffects
{
    /// <summary>The bitmaps a shape's glow is drawn as, outermost first.</summary>
    /// <param name="shape">The shape the glow belongs to.</param>
    /// <param name="glow">The glow.</param>
    /// <param name="page">The slide's size, which the bitmap is clipped against.</param>
    /// <param name="rasteriser">The sink that renders a picture's pixels, or null.</param>
    /// <param name="draw">Draws the shape's geometry, for its pixels.</param>
    public static IReadOnlyList<EffectLayer> Glow(
        PlacedShape shape,
        SlideGlow glow,
        DocSize page,
        IDrawingSink? rasteriser = null,
        Action<IDrawingSink>? draw = null)
    {
        EffectContent content = Geometry(shape, draw);

        // A picture's own pixels decide where it is, transparent ones included; geometry decides
        // everything else exactly.
        if (shape.Picture is { Image: not null } && draw is not null)
            content = new EffectContent
            {
                Fill = content.Fill, Stroke = content.Stroke, StrokeWidth = content.StrokeWidth,
                Draw = draw, MaskFromPixels = true,
            };

        return ShapeEffects.Glow(content, glow.Colour, glow.Radius, glow.Twice, Page(page), rasteriser);
    }

    /// <summary>
    /// The bitmap a softened shape is drawn as in place of its fill, picture and outline, or null
    /// when it is drawn as it is.
    /// </summary>
    public static EffectLayer? SoftEdge(
        PlacedShape shape,
        SlideSoftEdge softEdge,
        DocSize page,
        IDrawingSink? rasteriser = null,
        Action<IDrawingSink>? draw = null)
        => ShapeEffects.SoftEdge(Geometry(shape, draw), softEdge.Radius, softEdge.Twice, Page(page), rasteriser);

    /// <summary>The bitmap a shape's text glow is drawn as; empty without a rasteriser.</summary>
    /// <param name="text">The text that glows.</param>
    /// <param name="glow">The glow, its radius as stated.</param>
    /// <param name="page">The slide's size.</param>
    /// <param name="rasteriser">The sink that renders the text's pixels.</param>
    /// <param name="draw">Draws the text.</param>
    /// <remarks>
    /// <para>
    /// <strong>The radius is scaled by the text's size</strong>, as
    /// <c>createNewSdrGlowTextAttribute</c> does (<c>svx/source/sdr/primitive2d/sdrattributecreator.cxx</c>:212-260):
    /// <c>round(radius / (154.39 · size<sup>−0.575</sup>))</c> in hundredths of a millimetre, with
    /// the size the largest font height in the body, also in hundredths of a millimetre — an
    /// approximation of PowerPoint's own text-glow size that LibreOffice states as one.
    /// </para>
    /// <para>
    /// <strong>The content's range is the text's ink</strong>: the glow's range is the text
    /// primitive's, and a text portion's range is <c>getTextBoundRect</c>, its glyphs' own boxes.
    /// It is found by rendering the text once, finely, and reading where the ink is.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<EffectLayer> TextGlow(
        PlacedText text,
        SlideTextGlow glow,
        DocSize page,
        IDrawingSink rasteriser,
        Action<IDrawingSink> draw)
    {
        if (glow.Colour.A == 0 || glow.Radius <= Length.Zero) return [];

        Length size = glow.ShapeFontSize;
        foreach (PlacedGlyphRun run in text.Runs)
            if (run.Run.FontSize > size) size = run.Run.FontSize;
        if (size <= Length.Zero) return [];

        double stated = Math.Round(glow.Radius.Emu / 360.0);
        double height = Math.Round(size.Emu / 360.0);
        double radius = Math.Round(stated / (154.39 * Math.Pow(height, -0.575)));
        if (radius <= 0) return [];

        if (InkBounds(text, rasteriser, draw) is not { } ink) return [];

        EffectContent content = new() { Bounds = ink, Draw = draw, MaskFromPixels = true };
        return ShapeEffects.Glow(
            content, glow.Colour, Length.FromEmu((long)(radius * 360)), twice: false, Page(page), rasteriser);
    }

    /// <summary>
    /// The page-space box of a text's ink, read off a rendering of it at twenty pixels a point —
    /// fine enough that the box is a twentieth of a point from the glyphs' own.
    /// </summary>
    private static DocRect? InkBounds(PlacedText text, IDrawingSink rasteriser, Action<IDrawingSink> draw)
    {
        // Generous: a glyph reaches at most a body height above its baseline and below it, and
        // a little past its advance either side.
        DocRect? reach = null;
        foreach (PlacedGlyphRun placed in text.Runs)
        {
            GlyphRun run = placed.Run;
            Length advance = Length.Zero;
            foreach (PositionedGlyph glyph in run.Glyphs) advance += glyph.Advance;

            Length em = run.FontSize;
            DocRect box = new(
                run.Origin.X - em, run.Origin.Y - em - em, advance + em + em, em + em + em);
            reach = reach is { } r ? r.Union(box) : box;
        }

        if (reach is not { } area) return null;
        area = Transformed(area, text.Transform);

        const double PixelsPerPoint = 20.0;
        double scale = PixelsPerPoint / Length.EmuPerPoint;
        double pixels = area.Width.Emu * scale * area.Height.Emu * scale;
        if (pixels > 16_000_000) scale *= Math.Sqrt(16_000_000 / pixels);

        int width = Math.Max(1, (int)Math.Ceiling(area.Width.Emu * scale));
        int height = Math.Max(1, (int)Math.Ceiling(area.Height.Emu * scale));
        DocRect covered = new(area.X, area.Y, Length.FromEmu((long)Math.Round(width / scale)), Length.FromEmu((long)Math.Round(height / scale)));

        if (rasteriser.Rasterise(width, height, covered, draw) is not { IsDecoded: true } image) return null;

        ReadOnlySpan<byte> rgba = image.Pixels.Span;
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (rgba[(y * image.Width + x) * 4 + 3] == 0) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        if (right < left || bottom < top) return null;

        double perPixelX = covered.Width.Emu / (double)image.Width;
        double perPixelY = covered.Height.Emu / (double)image.Height;

        // A pixel with any coverage holds ink somewhere inside it; its middle is the best guess.
        return new DocRect(
            Length.FromEmu((long)Math.Round(covered.X.Emu + (left + 0.5) * perPixelX)),
            Length.FromEmu((long)Math.Round(covered.Y.Emu + (top + 0.5) * perPixelY)),
            Length.FromEmu((long)Math.Round((right - left) * perPixelX)),
            Length.FromEmu((long)Math.Round((bottom - top) * perPixelY)));
    }

    /// <summary>The page-space box of a rectangle under a transform.</summary>
    private static DocRect Transformed(DocRect rect, AffineTransform transform)
    {
        if (transform.IsIdentity) return rect;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach ((double x, double y) in (ReadOnlySpan<(double, double)>)[
                     (rect.Left.Emu, rect.Top.Emu), (rect.Right.Emu, rect.Top.Emu),
                     (rect.Left.Emu, rect.Bottom.Emu), (rect.Right.Emu, rect.Bottom.Emu)])
        {
            double px = transform.A * x + transform.C * y + transform.E;
            double py = transform.B * x + transform.D * y + transform.F;
            minX = Math.Min(minX, px); maxX = Math.Max(maxX, px);
            minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
        }

        return new DocRect(
            Length.FromEmu((long)minX), Length.FromEmu((long)minY),
            Length.FromEmu((long)(maxX - minX)), Length.FromEmu((long)(maxY - minY)));
    }

    /// <summary>The geometry a shape's effects are computed from, and its colour when it is one.</summary>
    private static EffectContent Geometry(PlacedShape shape, Action<IDrawingSink>? draw)
    {
        bool filled = shape.Fill is not null || shape.Picture is not null;
        bool solid = shape.Fill is SolidPaint && shape.Picture is null && shape.Line is null
                     && shape.ShadedParts.Count == 0;

        return new EffectContent
        {
            Fill = filled ? [shape.FillOutline] : [],
            Stroke = shape.Line is not null ? [shape.StrokeOutline] : [],
            StrokeWidth = shape.Line?.Width ?? Length.Zero,
            SolidColour = solid ? ((SolidPaint)shape.Fill!).Colour : null,
            Draw = solid ? null : draw,
        };
    }

    private static DocRect Page(DocSize page) => new(DocPoint.Origin, page);
}
