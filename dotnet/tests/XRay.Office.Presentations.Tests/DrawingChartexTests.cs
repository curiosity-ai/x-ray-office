using System.Xml.Linq;
using XRay.Office.Core.Charts;
using XRay.Office.Core.Graphics;
using XRay.Office.Ooxml.DrawingML;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// What the reader makes of an extended ("chartex") chart part, against the 26.8 daily.
/// </summary>
/// <remarks>
/// <para>
/// The first literals are the shape both corpus witnesses have —
/// <c>054_Problem_analysis_with_Pareto_chart_11058329.xlsx</c> and
/// <c>051_Manufacturer_defect_analysis_53db27ea.xlsx</c>, which are one template twice: two
/// <c>cx:data</c> blocks, four series alternating <c>clusteredColumn</c> and <c>paretoLine</c>.
/// 26.2.4.2 drew two polylines for them; 26.8 draws neither, because none of the four has a
/// plotter, and puts its notice over the axes instead. That template was rebuilt from
/// <c>chart2/qa/extras/data/xlsx/paretoLine.xlsx</c> at five, ten and twenty points and rendered
/// through the daily: no series mark, the notice, a value axis of 0 to 12.
/// </para>
/// <para>
/// The others are the QA documents' own parts, trimmed: <c>SimpleHistogram.xlsx</c>,
/// <c>paretoLine.xlsx</c>, <c>funnel1.xlsx</c> and <c>forum-mso-de-138303.pptx</c>.
/// </para>
/// </remarks>
public class DrawingChartexTests
{
    private const string CX = "http://schemas.microsoft.com/office/drawing/2014/chartex";
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>The two witnesses' shape, with the series list supplied.</summary>
    private static string Part(string series, string data = "", string axes = "")
        => $"""
            <cx:chartSpace xmlns:cx="{CX}" xmlns:a="{A}">
              <cx:chartData>
                <cx:data id="0">
                  <cx:strDim type="cat"><cx:f>Cats</cx:f></cx:strDim>
                  <cx:numDim type="val"><cx:f>Vals</cx:f></cx:numDim>
                </cx:data>
                <cx:data id="1">
                  <cx:strDim type="cat"><cx:f>WideCats</cx:f></cx:strDim>
                  <cx:numDim type="val"><cx:f>Cumulative</cx:f></cx:numDim>
                </cx:data>
                {data}
              </cx:chartData>
              <cx:chart><cx:plotArea><cx:plotAreaRegion>
                <cx:plotSurface><cx:spPr><a:ln><a:noFill/></a:ln></cx:spPr></cx:plotSurface>
                {series}
              </cx:plotAreaRegion>{axes}</cx:plotArea></cx:chart>
              <cx:spPr><a:noFill/><a:ln><a:noFill/></a:ln></cx:spPr>
            </cx:chartSpace>
            """;

    private const string TwoAxes =
        """
        <cx:axis id="0"><cx:catScaling gapWidth="0"/><cx:tickLabels/></cx:axis>
        <cx:axis id="1"><cx:valScaling/><cx:majorGridlines/><cx:tickLabels/></cx:axis>
        """;

    /// <summary>The workbook the two witnesses reference through hidden defined names.</summary>
    private static ChartRangeValues? Resolve(string formula) => formula switch
    {
        "Cats" => new ChartRangeValues(["a", "b", "c"], [null, null, null]),
        "WideCats" => new ChartRangeValues(["hdr", "a", "b", "c"], [null, null, null, null]),
        "Vals" => new ChartRangeValues(["35", "25", "21"], [35, 25, 21]),
        "Cumulative" => new ChartRangeValues(["0.5", "0.8", "1"], [0.5, 0.8, 1.0]),
        "Hist" => new ChartRangeValues(["12", "10", "11", "13", "15"], [12, 10, 11, 13, 15]),
        _ => null,
    };

    /// <summary>The range each hidden name stands for, as a workbook expands it.</summary>
    private static string Expand(string formula) => formula switch
    {
        "Hist" => "Sheet1!$A$1:$A$5",
        "Vals" => "Sheet1!$C$4:$C$6",
        _ => formula,
    };

    private static ChartPlot Require(string markup, bool workbook = true)
        => (workbook
               ? DrawingChartex.Read(XElement.Parse(markup), ranges: Resolve, expand: Expand)
               : DrawingChartex.Read(XElement.Parse(markup), automaticChartAreaLine: false))
           ?? throw new InvalidOperationException("the reader found nothing to draw");

    private const string TwoColumnsTwoParetos =
        """
        <cx:series layoutId="clusteredColumn" formatIdx="0">
          <cx:tx><cx:txData><cx:f>Head</cx:f><cx:v>OCCURRENCES</cx:v></cx:txData></cx:tx>
          <cx:spPr><a:solidFill><a:srgbClr val="4D62EF"/></a:solidFill></cx:spPr>
          <cx:dataId val="0"/><cx:axisId val="1"/>
        </cx:series>
        <cx:series layoutId="paretoLine" ownerIdx="0" formatIdx="1">
          <cx:axisId val="2"/>
        </cx:series>
        <cx:series layoutId="clusteredColumn" hidden="1" formatIdx="2">
          <cx:tx><cx:txData><cx:f>Head2</cx:f><cx:v>CUMULATIVE PERCENT</cx:v></cx:txData></cx:tx>
          <cx:dataId val="1"/><cx:axisId val="1"/>
        </cx:series>
        <cx:series layoutId="paretoLine" ownerIdx="2" formatIdx="3">
          <cx:axisId val="2"/>
        </cx:series>
        """;

    private const string Histogram =
        """
        <cx:series layoutId="clusteredColumn">
          <cx:dataId val="2"/><cx:layoutPr><cx:binning intervalClosed="r"/></cx:layoutPr>
        </cx:series>
        """;

    private const string HistogramData =
        """<cx:data id="2"><cx:numDim type="val"><cx:f>Hist</cx:f></cx:numDim></cx:data>""";

    /// <summary>
    /// The witnesses' template has no plotter for any of its series: nothing is drawn of them and
    /// the notice is.
    /// </summary>
    [Fact]
    public void AClusteredColumnWithoutBinningAndAParetoLineHaveNoPlotter()
    {
        ChartPlot plot = Require(Part(TwoColumnsTwoParetos, axes: TwoAxes));

        plot.Series.Count.ShouldBe(2);
        plot.Series.ShouldAllBe(series => series.IsUnsupported);
        plot.UnsupportedNotice.ShouldBe(DrawingChartex.UnsupportedNotice);
        plot.Series[0].Name.ShouldBe("OCCURRENCES");
    }

    /// <summary>
    /// A <c>clusteredColumn</c> with a <c>cx:binning</c> is a histogram over Scott's bins, with gap
    /// width zero and no notice.
    /// </summary>
    /// <remarks>
    /// SimpleHistogram.xlsx on the daily: <c>[10-13.94]</c> under a bar of 4 and
    /// <c>(13.94-17.87]</c> under a bar of 1, the bars touching.
    /// </remarks>
    [Fact]
    public void ABinnedClusteredColumnIsAHistogram()
    {
        ChartPlot plot = Require(Part(Histogram, HistogramData, TwoAxes));

        plot.Kind.ShouldBe(ChartPlotKind.Bar);
        plot.Categories.ShouldBe(["[10-13.94]", "(13.94-17.87]"]);
        plot.Series.Single().Values.ShouldBe([4.0, 1.0]);
        plot.Series.Single().IsUnsupported.ShouldBeFalse();
        plot.GapWidth.ShouldBe(0.0);
        plot.UnsupportedNotice.ShouldBeNull();
    }

    /// <summary>
    /// An unnamed series in a workbook is named by Calc from its range, and in a deck by the
    /// internal data provider.
    /// </summary>
    /// <remarks>
    /// tdf163727_histogram_roundtrip_multi_chart.xlsx's legend reads "Column A"; the waterfall of
    /// forum-mso-de-138303.pptx reads "Series1".
    /// </remarks>
    [Fact]
    public void AnUnnamedSeriesIsNamedByItsHost()
    {
        Require(Part(Histogram, HistogramData, TwoAxes)).Series.Single().Name.ShouldBe("Column A");

        string cached = Part(
            """<cx:series layoutId="waterfall"><cx:dataId val="3"/></cx:series>""",
            """
            <cx:data id="3"><cx:numDim type="val"><cx:lvl ptCount="2">
              <cx:pt idx="0">13</cx:pt><cx:pt idx="1">-5</cx:pt></cx:lvl></cx:numDim></cx:data>
            """,
            TwoAxes);
        Require(cached, workbook: false).Series.Single().Name.ShouldBe("Series1");
    }

    /// <summary>
    /// A chartex axis is a non-2007 model: an absent tick-mark element is cross, minor ticks
    /// included, and the axes are X and Y by position.
    /// </summary>
    [Fact]
    public void AnAbsentTickMarkIsCrossAndMinorTicksAreDrawn()
    {
        ChartPlot plot = Require(Part(Histogram, HistogramData, TwoAxes));

        plot.ValueTicks.ShouldBe(ChartTickMark.Cross);
        plot.CategoryTicks.ShouldBe(ChartTickMark.Cross);
        plot.ValueMinorTicks.ShouldBe(ChartTickMark.Cross);
        plot.CategoryMinorTicks.ShouldBe(ChartTickMark.Cross);
        plot.ValueGrid.ShouldNotBeNull();
        plot.ValueAxisVisible.ShouldBeTrue();
        plot.CategoryAxisVisible.ShouldBeTrue();
    }

    /// <summary>A part with no axes draws neither: a treemap is its title and its notice.</summary>
    [Fact]
    public void APartWithNoAxesDrawsNone()
    {
        ChartPlot plot = Require(Part(
            """<cx:series layoutId="treemap"><cx:dataId val="0"/></cx:series>"""));

        plot.ValueAxisVisible.ShouldBeFalse();
        plot.CategoryAxisVisible.ShouldBeFalse();
        plot.UnsupportedNotice.ShouldNotBeNull();
    }

    /// <summary>
    /// A pareto's column series is alone in the first axes set and so is a histogram, measured
    /// against a scale nobody draws; the axes shown are the second set's, which carry no grid.
    /// </summary>
    /// <remarks>
    /// paretoLine.xlsx on the daily: bars of 6 and 4 under a visible axis of 0 to 12 whatever the
    /// data, no horizontal gridline although axis 1 states one, and the notice over the bars.
    /// </remarks>
    [Fact]
    public void AParetoIsAHiddenScaleHistogramUnderTheSecondSetsAxes()
    {
        ChartPlot plot = Require(Part(
            Histogram + """<cx:series layoutId="paretoLine" ownerIdx="0"><cx:axisId val="2"/></cx:series>""",
            HistogramData,
            TwoAxes));

        ChartSeries bars = plot.Series.Single();
        bars.IsUnsupported.ShouldBeFalse();
        bars.AxisIndex.ShouldBe(1);
        plot.SecondaryAxisVisible.ShouldBeFalse();
        plot.ValueAxisVisible.ShouldBeTrue();
        plot.ValueGrid.ShouldBeNull();
        plot.UnsupportedNotice.ShouldNotBeNull();
        plot.Categories.ShouldBe(["[10-13.94]", "(13.94-17.87]"]);
    }

    /// <summary>
    /// A series stating no categories is given 1 … N, and a category axis' number format is not
    /// applied to them.
    /// </summary>
    /// <remarks>funnel1.xlsx states <c>0.00E+00</c> on its axis and the daily draws 1 to 6.</remarks>
    [Fact]
    public void AnUncategorisedSeriesIsNumberedAndItsCategoriesAreText()
    {
        ChartPlot plot = Require(Part(
            """<cx:series layoutId="funnel"><cx:dataId val="2"/></cx:series>""",
            HistogramData,
            """
            <cx:axis id="0"><cx:catScaling gapWidth="0.06"/><cx:majorTickMarks type="in"/>
              <cx:tickLabels/><cx:numFmt formatCode="0.00E+00" sourceLinked="0"/></cx:axis>
            """));

        plot.Categories.ShouldBe(["1", "2", "3", "4", "5"]);
        plot.CategoryFormat.ShouldBeNull();
        plot.CategoryTicks.ShouldBe(ChartTickMark.Inner);
        plot.ValueAxisVisible.ShouldBeFalse();
        plot.ShiftedCategories.ShouldBeFalse();
    }

    /// <summary>
    /// A chart area and a wall stating no fill are white in a workbook or a document and left
    /// alone in a deck.
    /// </summary>
    [Fact]
    public void AnUnstatedChartAreaIsWhiteOutsideImpress()
    {
        string part = $"""
            <cx:chartSpace xmlns:cx="{CX}" xmlns:a="{A}">
              <cx:chartData>{HistogramData}</cx:chartData>
              <cx:chart><cx:plotArea><cx:plotAreaRegion>{Histogram}</cx:plotAreaRegion>{TwoAxes}
              </cx:plotArea></cx:chart>
            </cx:chartSpace>
            """;

        ChartPlot sheet = Require(part);
        sheet.Background.ShouldBe(Colour.White);
        sheet.PlotBackground.ShouldBe(Colour.White);

        ChartPlot slide = DrawingChartex.Read(
            XElement.Parse(part), ranges: Resolve, automaticChartAreaLine: false)!;
        slide.Background.ShouldBeNull();
    }

    /// <summary>A part whose every series derives from another draws nothing.</summary>
    [Fact]
    public void APartWhoseSeriesNameNoDataIsNotDrawn()
        => DrawingChartex.Read(XElement.Parse(Part(
            """
            <cx:series layoutId="paretoLine" ownerIdx="0"><cx:axisId val="2"/></cx:series>
            """)), ranges: Resolve).ShouldBeNull();

    /// <summary>An ordinary <c>c:chartSpace</c> is not a chartex part.</summary>
    [Fact]
    public void APlainChartPartIsRefused()
        => DrawingChartex.Read(XElement.Parse(
            "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"/>"))
            .ShouldBeNull();
}
