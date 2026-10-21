using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;

namespace XRay.Office.WordProcessing.Layout;

/// <summary>
/// Glow and soft edge on a floating shape: what each effect is computed from, handed to
/// <see cref="ShapeEffects"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <c>wps:wsp</c> is an <c>SdrObjCustomShape</c> on Writer's draw page and a picture an
/// <c>SdrGrafObj</c>, and their effects are the drawing layer's, shared by every application:
/// measured on a 113 × 85 pt rectangle with an 18 pt red glow, 26.8 draws two images reaching 36 and
/// 18 pt, and with a 20 pt soft edge one faded image, exactly as Impress does.
/// </para>
/// <para>
/// The bitmaps are in page coordinates, so a turned shape's geometry is turned before it is
/// measured. The area they are clipped to is not known here and is taken as unbounded: the
/// reference clips to the visible area grown by the radius, which only matters for a shape
/// within a radius or two of the page's edge.
/// </para>
/// </remarks>
internal static class FrameEffects
{
    /// <summary>
    /// Draws a frame's glow, and its soft edge in place of its ink when it has one; true when the
    /// soft edge was drawn and the frame's fill, picture and border must not be.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="filled">Its fill outline, or null for its rectangle.</param>
    /// <param name="stroked">Its stroke outline, or null for its rectangle.</param>
    /// <param name="turn">The rotation it is drawn through, or null when it is upright.</param>
    /// <param name="sink">Where to draw, and the rasteriser for content that needs pixels.</param>
    /// <param name="draw">Draws the frame's fill, picture and border.</param>
    public static bool Draw(
        PlacedFrame frame,
        GraphicsPath? filled,
        GraphicsPath? stroked,
        AffineTransform? turn,
        IDrawingSink sink,
        Action<IDrawingSink> draw)
    {
        PageFrame shape = frame.Frame;
        if (shape.Glow is null && shape.SoftEdge is null) return false;
        if (frame.Ink.Width <= Length.Zero || frame.Ink.Height <= Length.Zero) return false;

        EffectContent content = Content(frame, filled, stroked, turn, draw);
        DocRect visible = new(
            frame.Ink.X - Length.FromPoints(100_000), frame.Ink.Y - Length.FromPoints(100_000),
            Length.FromPoints(200_000), Length.FromPoints(200_000));

        if (shape.Glow is { } glow)
        {
            foreach (EffectLayer layer in ShapeEffects.Glow(content, glow.Colour, glow.Radius, glow.Twice, visible, sink))
                sink.DrawImage(layer.Image, layer.Destination);
        }

        if (shape.SoftEdge is not { } soft) return false;
        if (ShapeEffects.SoftEdge(content, soft.Radius, soft.Twice, visible, sink) is not { } softened) return false;

        sink.DrawImage(softened.Image, softened.Destination);
        return true;
    }

    private static EffectContent Content(
        PlacedFrame frame, GraphicsPath? filled, GraphicsPath? stroked, AffineTransform? turn, Action<IDrawingSink> draw)
    {
        PageFrame shape = frame.Frame;
        bool picture = shape.Image is not null || shape.Vector is not null || shape.Chart is not null;
        bool bordered = shape.BorderColour is not null && shape.BorderWidth > Length.Zero;
        bool painted = shape.Fill is not null || shape.Gradient is not null || picture;
        bool solid = shape.Fill is not null && shape.Gradient is null && !picture && !bordered;

        GraphicsPath box = GraphicsPath.Rectangle(frame.Ink);

        return new EffectContent
        {
            Fill = painted ? [Turned(filled ?? box, turn)] : [],
            Stroke = bordered ? [Turned(stroked ?? filled ?? box, turn)] : [],
            StrokeWidth = shape.BorderWidth,
            SolidColour = solid ? shape.Fill : null,
            Draw = solid ? null : draw,
            MaskFromPixels = picture,
        };
    }

    private static GraphicsPath Turned(GraphicsPath path, AffineTransform? turn)
    {
        if (turn is not { } t) return path;

        DocPoint Apply(DocPoint p) => new(
            Length.FromEmu((long)Math.Round(t.A * p.X.Emu + t.C * p.Y.Emu + t.E)),
            Length.FromEmu((long)Math.Round(t.B * p.X.Emu + t.D * p.Y.Emu + t.F)));

        GraphicsPath turned = new();
        foreach (PathCommand command in path.Commands)
        {
            switch (command.Verb)
            {
                case PathVerb.MoveTo: turned.MoveTo(Apply(command.Point)); break;
                case PathVerb.LineTo: turned.LineTo(Apply(command.Point)); break;
                case PathVerb.CubicTo:
                    turned.CubicTo(Apply(command.Control1), Apply(command.Control2), Apply(command.Point));
                    break;
                case PathVerb.Close: turned.Close(); break;
                default: break;
            }
        }

        return turned;
    }
}
