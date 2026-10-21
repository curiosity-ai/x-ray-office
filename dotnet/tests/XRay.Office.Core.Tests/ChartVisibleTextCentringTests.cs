using XRay.Office.Core.Charts;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using Shouldly;

namespace XRay.Office.Core.Tests;

/// <summary>
/// A chart label ending in blanks keeps their width and has its glyphs centred without them,
/// which is what the 26.8 branch's EditEngine does to every centred line.
/// </summary>
/// <remarks>
/// <para>
/// tdf#168135 (<c>ImpEditEngine::CreateLines</c>, <c>editeng/source/editeng/impedit3.cxx:1646-1682</c>
/// on the 26.8 branch) centres a line on its width less its trailing U+0020. A chart label is a
/// centred paragraph in a shape autogrown to the whole line, so the box is unchanged and the glyphs
/// move half the blanks right inside it. Measured against the 26.8 daily on 2026-10-21: a category
/// written <c>"Q1   "</c> moves 4.08 pt further than its neighbours from 26.2.4.2 in a Writer
/// document and a workbook, three blanks' half being 4.07; an accounting value axis' <c>_)</c>,
/// 1.36 to 1.37 pt in all three hosts. See <c>ChartLayout.OnVisibleText</c>.
/// </para>
/// <para>
/// A legend entry is set flush left (<c>VLegend.cxx:118</c>), so its blanks change nothing.
/// </para>
/// </remarks>
public class ChartVisibleTextCentringTests
{
    /// <summary>Half an em per character, 1.15 em a line.</summary>
    private sealed class Ruler : IChartTextMeasurer
    {
        public DocSize Measure(string text, Length size, string? family, bool bold)
            => new(size * (0.5 * text.Length) * (bold ? 1.1 : 1.0), size * 1.15);
    }

    private static readonly DocRect Frame =
        new(Length.Zero, Length.Zero, Length.FromPoints(400), Length.FromPoints(300));

    private static ChartDrawing Place(ChartPlot plot) => ChartLayout.Place(plot, Frame, new Ruler());

    private static ChartPlot Bars(string first, string series) => new()
    {
        Categories = [first, "Q2", "Q3", "Q4"],
        Series = [new ChartSeries(series, [120.0, 95.0, 143.0, 168.0], Colour.FromRgb(0x99CCFF))],
        Legend = ChartLegendPosition.Right,
    };

    /// <summary>
    /// Three trailing blanks move a category label half their width right, and nothing else.
    /// </summary>
    [Fact]
    public void ACategoryLabelsTrailingBlanksMoveItsGlyphsHalfTheirWidth()
    {
        ChartDrawing plain = Place(Bars("Q1", "North"));
        ChartDrawing blanks = Place(Bars("Q1   ", "North"));

        ChartLabel before = plain.Labels.Single(label => label.Text == "Q1");
        ChartLabel after = blanks.Labels.Single(label => label.Text == "Q1   ");

        Length size = after.Size;
        Length half = size * (0.5 * 3) / 2;

        (after.At.X - before.At.X).Emu.ShouldBeInRange(half.Emu - 200L, half.Emu + 200L);
        after.At.Y.ShouldBe(before.At.Y);

        // The neighbours are where they were.
        blanks.Labels.Single(label => label.Text == "Q2").At
            .ShouldBe(plain.Labels.Single(label => label.Text == "Q2").At);
    }

    /// <summary>A legend entry is left-aligned, so its blanks move nothing.</summary>
    [Fact]
    public void ALegendEntrysTrailingBlanksMoveNothing()
    {
        ChartDrawing plain = Place(Bars("Q1", "North"));
        ChartDrawing blanks = Place(Bars("Q1", "North   "));

        ChartLabel before = plain.Labels.Single(label => label.Text == "North");
        ChartLabel after = blanks.Labels.Single(label => label.Text == "North   ");

        after.IsLeftAligned.ShouldBeTrue();

        // The legend itself is wider by the blanks and so starts further left; what must not move
        // is the name against its own key.
        (after.At.X - Key(blanks).Right).ShouldBe(before.At.X - Key(plain).Right);
    }

    /// <summary>The legend's one key, the only box in the series' colour right of the diagram.</summary>
    private static DocRect Key(ChartDrawing drawing)
        => drawing.Boxes
            .Where(box => box.Fill == Colour.FromRgb(0x99CCFF) && box.Bounds.X > drawing.DiagramArea.Right)
            .Single()
            .Bounds;
}
