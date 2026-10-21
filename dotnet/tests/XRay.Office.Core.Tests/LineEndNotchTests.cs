using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using Shouldly;

namespace XRay.Office.Core.Tests;

/// <summary>
/// A marker with a notched back stops its shaft at the notch, as the 26.8 branch does.
/// </summary>
/// <remarks>
/// <c>basegfx</c>'s <c>createAreaGeometryForLineStartEnd</c> gained the rule in upstream
/// <c>a0c13790095</c> (in 26.8.1.1 and on the 26.8 branch, not in 26.2.4.2). Measured on
/// <c>slide-shape-features.pptx</c> slide 3, a large stealth tail on a 3 pt line ending at 504 pt:
/// the daily's shaft ends at <b>495.978</b> where 26.2.4.2's ended at 489.969 — 0.6 of the 15 pt
/// marker less the 1 pt overlap, against its whole length less the overlap.
/// </remarks>
public sealed class LineEndNotchTests
{
    private static readonly Stroke Pen = new(Paint.Solid(Colour.FromRgb(0x000000)), Length.FromPoints(3));

    private static double ShaftEnd(string type)
    {
        GraphicsPath line = new GraphicsPath()
            .MoveTo(new DocPoint(Length.FromPoints(100), Length.Zero))
            .LineTo(new DocPoint(Length.FromPoints(504), Length.Zero));

        (GraphicsPath shaft, _) = LineEnds.Apply(line, Pen, default, new LineEnd(type, "lg", "lg"));
        return shaft.Commands[^1].Point.X.Points;
    }

    /// <summary>The stealth marker is crossed by its centre line at 60 % of its length.</summary>
    [Fact]
    public void AStealthShaftStopsAtTheNotch() => ShaftEnd("stealth").ShouldBe(496.0, 0.01);

    /// <summary>A triangle has no notch, so its shaft is shortened by its whole length as before.</summary>
    [Fact]
    public void ATriangleShaftIsShortenedByTheWholeMarker() => ShaftEnd("triangle").ShouldBe(490.0, 0.01);

    /// <summary>The open arrow's own shaft runs to its back, so it is not a notch either.</summary>
    [Fact]
    public void AnOpenArrowIsNotTreatedAsNotched()
        => ShaftEnd("arrow").ShouldBe(504 - (5.5 * 3) + (5.5 * 3 / 15), 0.01);
}
