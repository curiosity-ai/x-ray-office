using XRay.Office.Core;
using XRay.Office.Core.Charts;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Presentations.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// Checks what a chart on a slide draws, against numbers read out of LibreOffice's own PDF.
/// </summary>
/// <remarks>
/// <para>
/// The corpus deck is <c>chart-bar-deck.{odp,fodp,pptx}</c> — one hand-written 22 × 12 cm
/// clustered column chart and LibreOffice's two conversions of it — holding two series of four
/// values each over the categories Q1 to Q4, with a title, both axis titles and a legend on the
/// right. LibreOffice's PDF for it draws, in this order: a white chart-area rectangle, a grey
/// wall, five category ticks, ten value ticks labelled 0 to 180, the two axis lines, eight bars
/// each followed by its own outline, and the labels. The 26.8 daily (2026-10-21) draws the wall
/// from 108.85 to 606.78 pt across and 118.54 to 359.38 up.
/// </para>
/// <para>
/// <strong>Both families are now composed, and that is the finding of the retarget.</strong> The
/// ODP states its inner plot rectangle in <c>chart:coordinate-region</c>, and these tests used to
/// assert that rectangle exactly — which agreed with 26.2.4.2 only because the file was written
/// with 26.2's own automatic layout. Its plot area's style says <c>chart:auto-position</c> and
/// <c>chart:auto-size</c>, under which LibreOffice ignores the stated rectangle
/// (<c>OdfChartPlot.Region</c>), and the daily's automatic layout puts the wall 2.3 pt right of
/// it. So the ODP goes through the same composition as the PPTX, and both land within a tenth of
/// a point of the daily since the title insets were retargeted (<c>ChartLayout.TitleInsetEm</c>).
/// </para>
/// </remarks>
public class SlideChartDrawingTests
{
    /// <summary>
    /// A tenth and a half of a point, for the plot rectangle the composition computes on this deck.
    /// </summary>
    /// <remarks>
    /// It was two points against 26.2.4.2, which the composition missed by 1.29 pt left, 0.75
    /// below, 0.50 narrower and 0.76 taller. Measured on 2026-10-21 against the 26.8 daily, once
    /// a title's insets are sized by the model's 12 pt default (<c>ChartLayout.TitleInsetEm</c>):
    /// the composed wall is within 0.07 pt of the daily's on every edge and every bar within 0.05
    /// across and 0.08 in height, for the <c>.odp</c> and the <c>.pptx</c> alike. Most of the old
    /// residual was that rule under another reference's figures.
    /// </remarks>
    private const double ComposedTolerance = 0.15;

    private static LaidOutSlide Slide(string name)
    {
        using IDocument document =
            new PresentationReader().Read(DocumentSource.FromFile(Corpus.Require(name)));

        return ((SlidePages)((IPaginatedDocument)document).Layout()).Slides[0];
    }

    /// <summary>The bars: the shapes filled in the series' colour, in drawing order.</summary>
    private static List<DocRect> Bars(LaidOutSlide slide)
        => [.. slide.Shapes
            .Where(shape => shape.Fill == Paint.Solid(Colour.FromRgb(0x99CCFF)))
            .Select(Bounds)
            .Where(bounds => bounds.Width.Points > 20)];

    private static DocRect Bounds(PlacedShape shape)
    {
        double left = double.MaxValue, top = double.MaxValue;
        double right = double.MinValue, bottom = double.MinValue;

        foreach (PathCommand command in shape.Outline.Commands)
        {
            if (command.Verb == PathVerb.Close) continue;
            left = Math.Min(left, command.Point.X.Points);
            top = Math.Min(top, command.Point.Y.Points);
            right = Math.Max(right, command.Point.X.Points);
            bottom = Math.Max(bottom, command.Point.Y.Points);
        }

        return new DocRect(
            Core.Units.Length.FromPoints(left),
            Core.Units.Length.FromPoints(top),
            Core.Units.Length.FromPoints(right - left),
            Core.Units.Length.FromPoints(bottom - top));
    }

    [Fact]
    public void AnAutomaticOdfChartsPlotAreaIsComposedAndNotTheRectangleTheFileStates()
    {
        // chart-bar-deck.odp states chart:coordinate-region svg:x="2.258cm" — 64.01 pt into the
        // chart — under a plot-area style saying chart:auto-position="true", so LibreOffice lays
        // it out itself. The 26.8 daily (2026-10-21) draws the wall at 108.85 to 606.78 pt
        // across and 240.84 tall, 2.3 pt right of the stated rectangle; with both flags rewritten
        // false it draws the stated one. See OdfChartPlot.Region.
        LaidOutSlide slide = Slide("chart-bar-deck.odp");

        DocRect wall = slide.Shapes
            .Where(shape => shape.Fill == Paint.Solid(Colour.FromRgb(0xD9D9D9)))
            .Select(Bounds)
            .Single();

        wall.X.Points.ShouldBe(108.85, ComposedTolerance);
        wall.Right.Points.ShouldBe(606.78, ComposedTolerance);
        wall.Width.Points.ShouldBe(497.93, ComposedTolerance);
        wall.Height.Points.ShouldBe(240.84, ComposedTolerance);
    }

    [Fact]
    public void AnOdfChartsBarsLandWhereLibreOfficeDrawsThem()
    {
        // Every bar of chart-bar-deck.odp, quoted from the 26.8 daily's PDF (2026-10-21): the
        // four of the first series then the four of the second, each 41.50 pt wide, all rising
        // from 118.57.
        (double Left, double Height)[] reference =
        [
            (129.57, 160.56), (254.07, 127.11), (378.54, 191.34), (503.04, 224.79),
            (171.07, 117.75), (295.57, 176.63), (420.04, 135.16), (544.53, 161.92),
        ];

        List<DocRect> bars = Bars(Slide("chart-bar-deck.odp"));
        bars.Count.ShouldBe(8);

        for (int at = 0; at < reference.Length; at++)
        {
            bars[at].X.Points.ShouldBe(reference[at].Left, ComposedTolerance);
            bars[at].Width.Points.ShouldBe(41.50, ComposedTolerance);
            bars[at].Height.Points.ShouldBe(reference[at].Height, ComposedTolerance);
        }
    }

    [Fact]
    public void ABarsWidthIsItsSlotDividedByTheSeriesCountPlusTheGap()
    {
        // A category slot is the plot area's width over four categories, 124.48 pt on the daily's
        // 497.93 pt wall, and two clustered series with c:gapWidth val="100" divide it into
        // three — CategoryPositionHelper::getScaledSlotWidth with setOuterDistance(1.0). The first
        // bar starts half a gap into the slot. Both come straight out of the daily's numbers
        // (2026-10-21): 41.50 wide, starting 20.72 into a slot that starts at 108.85.
        List<DocRect> bars = Bars(Slide("chart-bar-deck.odp"));

        bars[0].Width.Points.ShouldBe(124.4825 / 3.0, ComposedTolerance);
        (bars[0].X.Points - 108.85).ShouldBe(124.4825 / 3.0 / 2.0, ComposedTolerance);

        // And the second series' first bar butts straight onto the first's, because c:overlap
        // is zero: no gap within a category, one bar's width of gap between categories.
        bars[4].X.Points.ShouldBe(bars[0].X.Points + bars[0].Width.Points, ComposedTolerance);
    }

    [Fact]
    public void ABarsHeightIsItsValueOverTheAxisRange()
    {
        // The check that catches a wrong axis scale, which is the failure that looks reasonable.
        // The plot is 240.84 pt for 180 units on the 26.8 daily (2026-10-21; 241.994 on
        // 26.2.4.2), so 120 is 160.56 pt and 168 is 224.78 — and an axis that ran to 200 instead
        // would draw them at 144.5 and 202.3.
        List<DocRect> bars = Bars(Slide("chart-bar-deck.odp"));

        double perUnit = bars[3].Height.Points / 168.0;
        bars[0].Height.Points.ShouldBe(120 * perUnit, 0.02);
        bars[1].Height.Points.ShouldBe(95 * perUnit, 0.02);
        bars[2].Height.Points.ShouldBe(143 * perUnit, 0.02);

        (240.84 / 180.0).ShouldBe(perUnit, 0.01);
    }

    [Fact]
    public void AnOoxmlChartIsComposedAndLandsWithinATenthOfAPointOfTheReference()
    {
        // The same chart as .pptx, whose c:plotArea has no c:layout at all — so this is the
        // composition heuristic's output rather than the file's. The residual is recorded on
        // ComposedTolerance and is entirely in the plot rectangle: the bars inside it are in the
        // same proportions as the ODF pair's, to a fortieth of a point.
        List<DocRect> bars = Bars(Slide("chart-bar-deck.pptx"));
        bars.Count.ShouldBe(8);

        // The daily's figures (2026-10-21), which this now meets to a tenth of a point.
        bars[0].X.Points.ShouldBe(129.57, ComposedTolerance);
        bars[0].Width.Points.ShouldBe(41.50, ComposedTolerance);
        bars[0].Height.Points.ShouldBe(160.52, ComposedTolerance);
        bars[3].Height.Points.ShouldBe(224.76, ComposedTolerance);

        double perUnit = bars[3].Height.Points / 168.0;
        bars[0].Height.Points.ShouldBe(120 * perUnit, 0.025);
    }

    [Fact]
    public void AnUnstyledOdfSeriesIsDrawnInTheImportersOwnDefaultBlue()
    {
        // chart-bar-deck.odp's series style ch9 states a chart-properties and a text-properties
        // and no graphic-properties whatever, so there is no fill in the file at all. ODF's
        // chart import defaults one — 0x0099ccff, "blue 8"
        // (xmloff/source/chart/ColorPropertySet.cxx:81) — and LibreOffice's PDF paints ten
        // rectangles in 0.6 0.8 1, which is that colour: eight bars and two legend keys.
        // Without the default the chart draws its axes and no bars, which reads as a data bug.
        Slide("chart-bar-deck.odp").Shapes
            .Count(shape => shape.Fill == Paint.Solid(Colour.FromRgb(0x99CCFF)))
            .ShouldBe(10);
    }

    [Fact]
    public void TheAxesTheirTicksAndTheirLabelsAreAllDrawn()
    {
        LaidOutSlide slide = Slide("chart-bar-deck.odp");

        // Two axis lines, five category ticks and ten value ticks: seventeen strokes, which is
        // exactly what LibreOffice's PDF writes for this chart.
        slide.Shapes.Count(shape => shape.Line is not null && shape.Fill is null).ShouldBe(17);

        // And the ticks' labels are real glyph runs rather than nothing, so a chart's numbers
        // survive pdftotext — which is what makes the whole-file word-count check meaningful.
        string text = string.Join(
            ' ',
            slide.Shapes
                .Where(shape => shape.Text is not null)
                .SelectMany(shape => shape.Text!.Runs)
                .Select(run => run.Run.Text));

        text.ShouldContain("180");
        text.ShouldContain("Q1");
        text.ShouldContain("Regional revenue");
        text.ShouldContain("Quarter");
        text.ShouldContain("North");
    }

    [Fact]
    public void AFlatOdfChartDrawsTheChartRatherThanItsMarkup()
    {
        // chart-bar-deck.fodp inlines the whole chart sub-document inside the draw:object, and
        // before charts were read a descendant search for text:p turned it into eighteen words
        // stacked as paragraphs. It now draws the same eight bars its packaged twin does.
        Bars(Slide("chart-bar-deck.fodp")).Count.ShouldBe(8);
    }

    /// <summary>The value axis' title is turned anticlockwise, so it reads bottom to top.</summary>
    /// <remarks>
    /// <c>ChartLabel.Rotation</c> is anticlockwise and slide coordinates grow downwards, so the
    /// transform that carries it must negate. Handing the angle straight to
    /// <c>AffineTransform.Rotation</c> turns every rotated piece of chart text the other way —
    /// measured on a probe whose axis title reads <c>Alpha Omega</c>, which came out top to
    /// bottom against LibreOffice's bottom to top, and on <c>Demick_JetBlue.pptx</c>, whose 45
    /// degree category labels descended to the right against the reference's ascending.
    /// </remarks>
    [Fact]
    public void TheValueAxisTitleIsTurnedAnticlockwise()
    {
        LaidOutSlide slide = Slide("features/chart-bar-deck.pptx");

        List<AffineTransform> turned =
            [.. slide.Shapes
                .Select(shape => shape.Text)
                .Where(text => text is { IsUpright: false })
                .Select(text => text!.Transform)
                // A non-identity transform is not necessarily a turned one: an autofit chart
                // label carries a horizontal stretch, whose B is exactly zero.
                .Where(transform => Math.Abs(transform.B) > 0.001)];

        turned.ShouldNotBeEmpty();

        // The text's own advance direction is +x. Under a quarter turn anticlockwise in a
        // y-down space it must come out pointing at -y, which is up the page.
        foreach (AffineTransform transform in turned)
        {
            (transform.A * transform.A + transform.B * transform.B).ShouldBe(1.0, 0.001);
            transform.B.ShouldBeLessThan(0.0);
        }
    }
}
