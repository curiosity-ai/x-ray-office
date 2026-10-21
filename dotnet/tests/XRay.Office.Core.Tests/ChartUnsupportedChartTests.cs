using XRay.Office.Core.Charts;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using Shouldly;

namespace XRay.Office.Core.Tests;

/// <summary>
/// What <c>UnsupportedChart</c> draws, and the minor ticks a chartex axis carries — both measured on
/// the chartex QA documents through the 26.8 daily.
/// </summary>
public class ChartUnsupportedChartTests
{
    /// <summary>Half an em per character and 1.125 em of height, like a 10 pt Liberation Serif line.</summary>
    private sealed class Ruler : IChartTextMeasurer
    {
        public DocSize Measure(string text, Length size, string? family, bool bold)
            => new(size * (0.5 * text.Length), size * 1.125);
    }

    private static readonly DocRect Frame =
        new(Length.Zero, Length.Zero, Length.FromPoints(400), Length.FromPoints(300));

    private const string Notice =
        "One or more chart types are unsupported in this version of LibreOffice";

    private static ChartPlot Waterfall(bool unsupported) => new()
    {
        Kind = ChartPlotKind.Line,
        Categories = ["1", "2", "3", "4"],
        Series =
        [
            new ChartSeries("Column C", [2.0, 5.0, -4.0, 6.0], Line: Colour.FromRgb(0x4472C4))
            {
                IsUnsupported = unsupported,
            },
        ],
        Legend = ChartLegendPosition.Top,
        UnsupportedNotice = unsupported ? Notice : null,
    };

    /// <summary>
    /// The notice is centred on the frame, wrapped at two thirds of its width, in Liberation Serif
    /// at 10 pt over a white box exactly its own size.
    /// </summary>
    /// <remarks>
    /// funnel1.xlsx on the daily: a 283.64 pt frame centred at (312.89, 663.80) draws a
    /// 185.30 × 22.45 box centred at (312.90, 663.79) holding two lines.
    /// </remarks>
    [Fact]
    public void TheNoticeIsCentredOnTheFrameAndWrappedAtTwoThirdsOfIt()
    {
        ChartDrawing drawing = ChartLayout.Place(Waterfall(true), Frame, new Ruler());

        List<ChartLabel> lines = [.. drawing.Labels.Where(l => l.Family == "Liberation Serif")];
        lines.Count.ShouldBe(2);
        lines.ShouldAllBe(l => l.Size == Length.FromPoints(10));
        string.Join(" ", lines.Select(l => l.Text)).ShouldBe(Notice);
        foreach (ChartLabel line in lines)
        {
            (line.Text.Length * 5.0).ShouldBeLessThanOrEqualTo(400 * 2.0 / 3.0);
            line.At.X.Points.ShouldBe(200, 0.01);
        }

        // The two lines straddle the frame's middle, one line-height apart.
        ((lines[0].At.Y.Points + lines[1].At.Y.Points) / 2).ShouldBe(150, 0.01);
        (lines[1].At.Y.Points - lines[0].At.Y.Points).ShouldBe(11.25, 0.01);
    }

    /// <summary>
    /// The box is a shape, so it paints over the gridlines and the axes as the plotter's shape does,
    /// and it is white inside and out.
    /// </summary>
    [Fact]
    public void TheNoticeBoxIsAWhiteShapePaintedAfterTheGrid()
    {
        ChartDrawing drawing = ChartLayout.Place(Waterfall(true), Frame, new Ruler());

        drawing.Shapes.Count(s => s.Fill == Colour.White && s.Line == Colour.White).ShouldBe(1);
    }

    /// <summary>A series with no plotter draws no mark, and keys itself with a filled circle.</summary>
    [Fact]
    public void AnUnsupportedSeriesDrawsNoMarkAndACircleKey()
    {
        ChartDrawing drawn = ChartLayout.Place(Waterfall(false), Frame, new Ruler());
        ChartDrawing unsupported = ChartLayout.Place(Waterfall(true), Frame, new Ruler());

        // The line chart's polyline and its legend's line sample are gone; one filled circle the
        // series' colour is the key; the axes are unchanged.
        drawn.Shapes.Count(s => s.Line == Colour.FromRgb(0x4472C4)).ShouldBeGreaterThan(0);
        unsupported.Shapes.Count(s => s.Fill == Colour.FromRgb(0x4472C4) && s.Line is null)
            .ShouldBe(1);
        unsupported.Lines.Count(l => l.Colour == Colour.FromRgb(0x4472C4)).ShouldBe(0);
        unsupported.PlotArea.ShouldNotBe(DocRect.Empty);
    }

    /// <summary>Minor ticks are three quarters of a major one, five to a value interval.</summary>
    [Fact]
    public void MinorTicksAreThreeQuartersOfAMajorTick()
    {
        ChartPlot plot = Waterfall(false) with
        {
            ValueTicks = ChartTickMark.Cross,
            ValueMinorTicks = ChartTickMark.Cross,
            CategoryTicks = ChartTickMark.None,
            Legend = ChartLegendPosition.None,
        };

        ChartDrawing drawing = ChartLayout.Place(plot, Frame, new Ruler());

        // Vertical value axis: ticks are horizontal strokes about the axis line's x.
        List<double> lengths = [.. drawing.Lines
            .Where(l => l.From.Y == l.To.Y && (l.To.X - l.From.X).Points is > 0 and < 10)
            .Select(l => Math.Round((l.To.X - l.From.X).Points, 2))];

        double major = 2 * 4.252;
        lengths.Count(l => Math.Abs(l - major) < 0.02).ShouldBeGreaterThan(1);
        lengths.Count(l => Math.Abs(l - (major * 0.75)) < 0.02).ShouldBeGreaterThan(3);
    }
}
