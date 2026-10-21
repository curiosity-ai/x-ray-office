using System.Xml.Linq;
using XRay.Office.Core.Charts;
using XRay.Office.Core.Graphics;

namespace XRay.Office.Ooxml.DrawingML;

/// <summary>
/// Reads an extended ("chartex") chart part — <c>cx:chartSpace</c> — into a
/// <see cref="ChartPlot"/>, as the 26.8 reference draws it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Chartex is the 2014 vocabulary, and it is not <c>c:</c> with a different prefix.</strong>
/// There is no plot-group element: every <c>cx:series</c> carries a <c>@layoutId</c> naming its own
/// type, and the data lives in a <c>cx:chartData</c> block the series point at by id. LibreOffice
/// reads the layout ids in <c>PlotAreaContext::onCreateContext</c>
/// (<c>oox/source/drawingml/chart/plotareacontext.cxx</c>:170-247, 26-8 branch) and manufactures
/// one type group per series so that the rest of its chart machinery keeps working — which is why
/// this reader writes the <c>c:</c> part that machinery would have seen and hands it to
/// <see cref="DrawingChartPlot"/>. See <c>Synthesise</c>.
/// </para>
/// <para>
/// <strong>What 26.8 draws.</strong> A <c>clusteredColumn</c> with a <c>cx:binning</c> is a
/// histogram (tdf#165742: <c>loext:class="loext:histogram"</c> in the ODF export) — a bar chart over
/// Scott's-rule bins labelled <c>[10-13.94]</c>, <c>(13.94-17.87]</c>, with gap width zero and the
/// title, axes, gridlines and legend the part states; <see cref="ChartHistogram"/> is the binning.
/// Every other layout — funnel, waterfall, treemap, sunburst, box and whisker, region map, a
/// pareto's line and a <c>clusteredColumn</c> without binning — has no plotter
/// (<c>VSeriesPlotter::createSeriesPlotter</c>, <c>VSeriesPlotter.cxx</c>:2871-2909) and becomes
/// <c>UnsupportedChart</c>, which draws <see cref="UnsupportedNotice"/> and nothing of the series
/// while its points still scale the axes. A pareto is both: its column series sits alone in the
/// first axes set and so <em>is</em> a histogram, against a hidden scale, while the axes the page
/// shows are the second set's.
/// </para>
/// <para>
/// Measured on the QA documents in <c>chart2/qa/extras/data</c> and <c>sd/qa/unit/data</c> and on
/// authored variants through the 26.8 daily: on <c>SimpleHistogram.xlsx</c> 64 strokes and 18 text
/// runs on both sides with every word within 0.83 pt; on the six other histogram, pareto and
/// unsupported workbooks and decks the stroke counts agree exactly. 26.2.4.2 drew two polylines
/// here and nothing else; that was 26.2's drawing and is not reproduced any more.
/// </para>
/// <para>
/// <strong>The numbers are in the workbook, never in the part.</strong> A <c>c:</c> chart caches its
/// points; chartex has a cache element (<c>cx:lvl</c>/<c>cx:pt</c>), which a workbook's parts
/// usually omit and state a bare <c>cx:f</c> naming a hidden <c>_xlchart.v1.n</c> defined name
/// instead, while a deck's always carry it. The cache is read when it is there and the resolver is
/// asked otherwise, which is the same order <c>ExcelChartConverter::createDataSequence</c> uses for
/// <c>c:</c>.
/// </para>
/// </remarks>
public static class DrawingChartex
{
    /// <summary>The <c>a:graphicData/@uri</c> that identifies a chartex chart in a frame.</summary>
    /// <remarks>Identical to the chartex namespace, as OOXML does for every payload.</remarks>
    public const string ChartUri = OoxmlNamespaces.ExtendedChart;

    /// <summary>
    /// <c>STR_UNSUPPORTED_CHART_TYPE</c> with <c>%PRODUCTNAME</c> filled in as a release build
    /// fills it.
    /// </summary>
    /// <remarks>
    /// A development build says <c>LibreOfficeDev</c>, so a comparison against a daily differs by
    /// those three letters on this one line and by nothing else.
    /// </remarks>
    public const string UnsupportedNotice =
        "One or more chart types are unsupported in this version of LibreOffice";

    /// <summary>How many points one sequence is trusted to hold.</summary>
    private const int MaximumPoints = 65536;

    /// <summary>Reads a <c>cx:chartSpace</c>, or null when it holds nothing drawable.</summary>
    /// <param name="chartSpace">The part's root element.</param>
    /// <param name="theme">The theme <c>a:schemeClr</c> resolves against.</param>
    /// <param name="styles">The theme's format matrix, for the automatic colours and widths.</param>
    /// <param name="ranges">
    /// The workbook resolver a <c>cx:f</c> is put through. Null leaves a series with no values,
    /// which is what a chartex chart outside a workbook has unless the part caches its points.
    /// </param>
    /// <param name="expand">
    /// What a <c>cx:f</c> naming a defined name stands for, so an unnamed series can be given the
    /// <c>Column C</c> name Calc generates from its range. Null outside a workbook.
    /// </param>
    /// <param name="automaticChartAreaLine">
    /// Whether the chart area gets the grey default border when it states no line — every host but
    /// Impress; see <see cref="DrawingChartPlot.Read"/>.
    /// </param>
    public static ChartPlot? Read(
        XElement chartSpace,
        DrawingTheme? theme = null,
        DrawingStyleMatrix? styles = null,
        ChartRangeResolver? ranges = null,
        Func<string, string>? expand = null,
        bool automaticChartAreaLine = true)
    {
        ArgumentNullException.ThrowIfNull(chartSpace);
        if (!Is(chartSpace, "chartSpace")) return null;

        XElement? chart = Child(chartSpace, "chart");
        XElement? plotArea = Child(chart, "plotArea");
        XElement? region = Child(plotArea, "plotAreaRegion");
        if (region is null) return null;

        Dictionary<string, Dimension> data = ReadData(Child(chartSpace, "chartData"), ranges);

        // One fictitious type group per series, which is what PlotAreaContext builds
        // (plotareacontext.cxx:203-247, 26-8 branch).
        List<XElement> series = [.. Children(region, "series")];
        if (series.Count == 0) return null;

        // Every cx:axis is appended to the axis-id list of the type group created last
        // (plotareacontext.cxx:172-183), so only the last series carries axes; every earlier one
        // has an empty list. PlotAreaConverter groups type groups by equal lists
        // (plotareaconverter.cxx:414-451), so a single series is its own axes set with the axes,
        // and with several the earlier ones share a first set whose axes are created missing and
        // therefore deleted (lclGetOrCreateAxis, :110-116) while the last one is a second set —
        // the secondary axes — that carries every axis the part states.
        List<XElement> axes = [.. Children(plotArea, "axis")];
        bool split = series.Count > 1;

        // A clusteredColumn with a cx:binning is a histogram unless its own axes set also holds a
        // paretoLine (plotareaconverter.cxx:128-150). A pareto's column series sits alone in the
        // first set, so it *is* forced to a histogram; that is measured, below.
        bool[] histogram = new bool[series.Count];
        for (int at = 0; at < series.Count; at++)
        {
            bool last = at == series.Count - 1;
            bool paretoBeside = false;

            for (int other = 0; other < series.Count; other++)
            {
                bool sameSet = !split || ((other == series.Count - 1) == last);
                if (sameSet && LayoutOf(series[other]) == "paretoLine") paretoBeside = true;
            }

            histogram[at] = LayoutOf(series[at]) == "clusteredColumn"
                            && Child(Child(series[at], "layoutPr"), "binning") is not null
                            && !paretoBeside;
        }

        XElement synthetic = Synthesise(
            chartSpace, chart!, plotArea!, series, axes, data, histogram,
            out List<SeriesSource> drawn);

        if (drawn.Count == 0) return null;

        if (DrawingChartPlot.Read(
                synthetic, theme, office2007: false, styles, ranges: null,
                automaticChartAreaLine) is not { } plot)
        {
            return null;
        }

        if (plot.Series.Count != drawn.Count) return plot;

        List<ChartSeries> adjusted = new(plot.Series.Count);
        bool anyHistogram = false;

        // A series with no plotter draws the notice whether or not it has points: a pareto's line
        // states no cx:dataId at all and its UnsupportedChart still writes the text.
        bool anyUnsupported = Array.IndexOf(histogram, false) >= 0;

        for (int at = 0; at < drawn.Count; at++)
        {
            SeriesSource source = drawn[at];
            ChartSeries read = plot.Series[at];

            // An unnamed series is named by whichever data provider holds its points. Calc's
            // generates "Column C" from the range (see DrawingChartPlot.GeneratedSeriesName); a
            // deck's or a document's is the internal one, whose UncachedDataSequence::generateLabel
            // answers STR_DATA_UNNAMED_SERIES_WITH_INDEX, "Series%NUMBER", one-based
            // (chart2/source/tools/UncachedDataSequence.cxx:204-212). Measured on
            // forum-mso-de-138303.pptx, whose waterfall's legend reads "Series1" on the 26.8 daily.
            if (read.Name is null)
            {
                string? name = expand is not null
                    ? source.Formula is { } formula
                        ? DrawingChartPlot.GeneratedSeriesName(expand(formula))
                        : null
                    : "Series" + (at + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (name is not null) read = read with { Name = name };
            }

            // Measured against its own, invisible scale when the axes the reader can see are the
            // second set's. See below.
            if (split && !source.Last) read = read with { AxisIndex = 1 };

            if (!source.Histogram) read = read with { IsUnsupported = true };
            else anyHistogram = true;

            adjusted.Add(read);
        }

        // Every series with no plotter draws one notice between them, not one each: each
        // UnsupportedChart draws the same text at the same place, and the PDF holds it once per
        // type group. Measured on the pareto QA documents: one box, one pair of lines.
        ChartPlot result = plot with
        {
            Series = adjusted,
            UnsupportedNotice = anyUnsupported ? UnsupportedNotice : null,

            // HistogramChart::adaptGapWidthForHistogram zeroes the gap whatever catScaling states
            // (chart2/source/view/charttypes/HistogramChart.cxx:153-167, 26-8 branch).
            GapWidth = anyHistogram ? 0.0 : plot.GapWidth,

            // A chartex axis is a non-2007 AxisModel, so an absent cx:minorTickMarks is cross
            // (axiscontext.cxx:333-338, axismodel.cxx:42-44).
            ValueMinorTicks = TickOf(Child(AxisAt(axes, 1), "minorTickMarks"), AxisAt(axes, 1)),
            CategoryMinorTicks = TickOf(Child(AxisAt(axes, 0), "minorTickMarks"), AxisAt(axes, 0)),
        };

        // Outside Impress a chart area and a 2-D wall that state no fill are painted with the
        // graphic helper's default chart-area fill, which is solid (graphichelper.cxx:139-142,
        // against pptimport.cxx:309-312, and objectformatter.cxx:956-959 for the two object types).
        // The c: reader leaves that white unpainted on purpose — see
        // DrawingChartAutoFormat.FrameFillOf — but a chartex frame on a worksheet covers the cells
        // under it, and on the 26.8 daily every QA workbook's chart area and wall is a white fill.
        if (automaticChartAreaLine)
        {
            result = result with
            {
                Background = result.Background
                             ?? (StatesFill(Child(chartSpace, "spPr")) ? null : Colour.White),
                PlotBackground = result.PlotBackground
                                 ?? (StatesFill(Child(plotArea, "spPr")) ? null : Colour.White),
            };
        }

        if (split)
        {
            // The first set's axes are deleted and the second set's are what the page shows — at
            // the primary axes' places, because a chartex axis crosses at autoZero and the
            // category axis' zero is its start. So the reader's primary axes stand in for the
            // second set's, and the first set's series are measured against a scale nobody draws.
            // A secondary axis draws no grid (VCartesianGrid is created for the main axes only),
            // which the pareto QA document and a variant without cx:majorGridlines both show.
            //
            // Measured on paretoLine.xlsx through the 26.8 daily: the visible value axis runs 0 to
            // 12 for every data set tried — five, ten, twenty and fifty values, and the values
            // multiplied by ten — which is ScaleAutomatism's [0, 10] for an axis with no data,
            // expanded at its border; the bars stand against a hidden 0 to 7 for a tallest bin of 6.
            result = result with
            {
                SecondaryValueScale = new ChartScaleRequest(),
                SecondaryAxisVisible = false,
                SecondaryLabelsVisible = false,
                SecondaryTicks = ChartTickMark.None,
                ValueGrid = null,
                CategoryGrid = null,
                ValueMinorGrid = null,
                CategoryMinorGrid = null,
            };
        }

        return result;
    }

    /// <summary>Whether a <c>cx:spPr</c> states any fill at all, <c>a:noFill</c> included.</summary>
    private static bool StatesFill(XElement? properties)
    {
        if (properties is null) return false;

        foreach (XElement child in properties.Elements())
        {
            if (child.Name.NamespaceName == OoxmlNamespaces.DrawingML
                && child.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where one series of the synthesised part came from.</summary>
    private readonly record struct SeriesSource(bool Histogram, bool Last, string? Formula);

    private static string? LayoutOf(XElement series) => Attribute(series, "layoutId");

    private static XElement? AxisAt(List<XElement> axes, int index)
        => index < axes.Count ? axes[index] : null;

    /// <summary>
    /// A chartex tick-mark element as the reader's tick mark: <c>@type</c> defaulting to cross,
    /// and cross for an absent element on an axis that exists.
    /// </summary>
    private static ChartTickMark TickOf(XElement? marks, XElement? axis)
    {
        if (axis is null) return ChartTickMark.None;

        return (marks is null ? "cross" : Attribute(marks, "type") ?? "cross") switch
        {
            "none" => ChartTickMark.None,
            "in" => ChartTickMark.Inner,
            "out" => ChartTickMark.Outer,
            _ => ChartTickMark.Cross,
        };
    }

    /// <summary>
    /// Builds the <c>c:chartSpace</c> LibreOffice's importer turns a chartex part into, so that the
    /// title, legend, axis, gridline, text and colour rules the <c>c:</c> reader already ports
    /// apply to it unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The importer does exactly this, one element at a time.</strong> The chartex contexts
    /// fill the same <c>TitleModel</c>, <c>AxisModel</c>, <c>SeriesModel</c> and
    /// <c>LegendModel</c> the <c>c:</c> ones do, and the converters never learn which vocabulary
    /// they came from. Three rules decide the mapping and none of them is the element names:
    /// </para>
    /// <list type="bullet">
    /// <item>The axes are X and Y by <em>position</em> — the first <c>cx:axis</c> is the category
    /// axis and the second the value axis whatever scaling element each holds
    /// (<c>plotareaconverter.cxx</c>:434-441 with <c>axisconverter.cxx</c>:215-258), and an
    /// absent one is created deleted.</item>
    /// <item>A chartex axis is a non-2007 model, so a missing tick-mark element is
    /// <c>cross</c>; <c>cx:tickLabels</c> sets nothing, so labels are always shown; and the
    /// <c>hidden</c> attribute is a TODO (<c>plotareacontext.cxx</c>:175).</item>
    /// <item>A series' data is its <c>cx:dataId</c>'s block; one with none, a pareto line,
    /// has no points.</item>
    /// </list>
    /// <para>
    /// A histogram's series is written as a <c>c:barChart</c> over its bins and everything else as
    /// a <c>c:lineChart</c>, because neither plotter's marks are drawn for it and a line chart's
    /// categories sit on its ticks rather than between them, as a funnel's measured
    /// <c>1 … 6</c> do on the 26.8 daily.
    /// </para>
    /// </remarks>
    private static XElement Synthesise(
        XElement chartSpace,
        XElement chart,
        XElement plotArea,
        List<XElement> series,
        List<XElement> axes,
        Dictionary<string, Dimension> data,
        bool[] histogram,
        out List<SeriesSource> drawn)
    {
        XElement plot = new(C("plotArea"));
        drawn = [];

        for (int at = 0; at < series.Count; at++)
        {
            XElement element = series[at];
            Dimension dimension = default;
            bool hasData = Attribute(Child(element, "dataId"), "val") is { } id
                           && data.TryGetValue(id, out dimension)
                           && dimension.Values.Count > 0;

            // A series with no points draws nothing and scales nothing; the c: reader would drop
            // it anyway, and leaving it out keeps the two series lists in step.
            if (!hasData) continue;

            IReadOnlyList<string?> categories = dimension.Categories;
            IReadOnlyList<double?> values = dimension.Values;

            if (histogram[at])
            {
                ChartHistogramBins bins = ChartHistogram.Compute(values, BinningOf(element));
                categories = bins.Categories;
                values = bins.Frequencies;
            }

            XElement ser = new(C("ser"),
                Val("idx", at), Val("order", at));

            if (Child(Child(element, "tx"), "txData") is { } txData
                && Child(txData, "v")?.Value is { Length: > 0 } name)
            {
                ser.Add(new XElement(C("tx"), new XElement(C("v"), name)));
            }

            if (Child(element, "spPr") is { } shape) ser.Add(new XElement(C("spPr"), shape.Nodes()));

            if (histogram[at] && Child(element, "dataLabels") is { } labels)
                ser.Add(DataLabels(labels));

            // A series stating no cx:strDim still has categories: ExplicitCategoriesProvider
            // generates 1, 2, 3 … for the axis (lcl_getGeneratedCategories). Measured on
            // funnel1.xlsx, whose category axis reads 1 to 6 on the 26.8 daily.
            if (categories.Count == 0)
            {
                string?[] generated = new string?[values.Count];
                for (int point = 0; point < generated.Length; point++)
                    generated[point] = (point + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                categories = generated;
            }

            ser.Add(new XElement(C("cat"), StringLiteral(categories)));
            ser.Add(new XElement(C("val"), NumberLiteral(values)));

            XElement group = histogram[at]
                ? new XElement(C("barChart"),
                    Val("barDir", "col"), Val("grouping", "clustered"), Val("varyColors", 0), ser,
                    Val("gapWidth", 0), Val("overlap", 0))
                : new XElement(C("lineChart"),
                    Val("grouping", "standard"), Val("varyColors", 0), ser);

            group.Add(Val("axId", 1), Val("axId", 2));
            plot.Add(group);

            drawn.Add(new SeriesSource(
                histogram[at], at == series.Count - 1, dimension.Formula));
        }

        // With one axes set the part's axes are the chart's; with two the reader draws the second
        // set's at the primary places — see Read — so either way the c: axes are the cx ones.
        plot.Add(Axis("catAx", 1, 2, "b", AxisAt(axes, 0)));

        // A chartex axis states no crossBetween, so ShiftedCategoryPosition falls to the type:
        // shifted for the histogram (a bar), and not for any of the others, whose type category
        // is neither a bar nor a line (axisconverter.cxx:240-249). The c: reader shifts a line
        // chart with the element absent, so the unshifted answer is stated.
        XElement value = Axis("valAx", 2, 1, "l", AxisAt(axes, 1));
        if (Array.IndexOf(histogram, true) < 0) value.Add(Val("crossBetween", "midCat"));
        plot.Add(value);

        if (Child(plotArea, "spPr") is { } plotShape)
            plot.Add(new XElement(C("spPr"), plotShape.Nodes()));

        XElement converted = new(C("chart"));

        if (Child(chart, "title") is { } title) converted.Add(Title(title));
        else converted.Add(Val("autoTitleDeleted", 1));

        converted.Add(plot);

        if (Child(chart, "legend") is { } legend)
        {
            XElement written = new(C("legend"),
                Val("legendPos", Attribute(legend, "pos") ?? "r"),
                Val("overlay", Attribute(legend, "overlay") is "1" or "true" ? 1 : 0));
            if (Child(legend, "spPr") is { } legendShape)
                written.Add(new XElement(C("spPr"), legendShape.Nodes()));
            if (Child(legend, "txPr") is { } legendText)
                written.Add(new XElement(C("txPr"), legendText.Nodes()));
            converted.Add(written);
        }

        XElement result = new(C("chartSpace"), converted);
        if (Child(chartSpace, "spPr") is { } spaceShape)
            result.Add(new XElement(C("spPr"), spaceShape.Nodes()));
        if (Child(chartSpace, "txPr") is { } spaceText)
            result.Add(new XElement(C("txPr"), spaceText.Nodes()));

        return result;
    }

    /// <summary>A chartex axis, or a deleted one when the part states none at that position.</summary>
    private static XElement Axis(string kind, int id, int crosses, string position, XElement? axis)
    {
        XElement scaling = new(C("scaling"), Val("orientation", "minMax"));
        XElement written = new(C(kind), Val("axId", id), scaling);

        if (axis is null)
        {
            written.Add(
                Val("delete", 1), Val("axPos", position), Val("majorTickMark", "none"),
                Val("minorTickMark", "none"), Val("tickLblPos", "none"), Val("crossAx", crosses));
            return written;
        }

        // valScaling's limits reach only an axis in the value position; a category axis has none.
        if (kind == "valAx" && Child(axis, "valScaling") is { } values)
        {
            if (Attribute(values, "max") is { } max) scaling.Add(Val("max", max));
            if (Attribute(values, "min") is { } min) scaling.Add(Val("min", min));
        }

        written.Add(Val("delete", 0), Val("axPos", position));

        if (Child(axis, "majorGridlines") is { } major)
            written.Add(new XElement(C("majorGridlines"), Shape(major)));
        if (Child(axis, "minorGridlines") is { } minor)
            written.Add(new XElement(C("minorGridlines"), Shape(minor)));
        if (Child(axis, "title") is { } title) written.Add(Title(title));

        // A chartex category is text — a cx:strDim, a generated index or a histogram bin — so a
        // number format on the category axis has nothing to format: funnel1.xlsx states
        // 0.00E+00 there and the 26.8 daily draws 1 to 6.
        if (kind == "valAx" && Child(axis, "numFmt") is { } format)
        {
            written.Add(new XElement(C("numFmt"),
                new XAttribute("formatCode", Attribute(format, "formatCode") ?? "General"),
                new XAttribute("sourceLinked", Attribute(format, "sourceLinked") ?? "0")));
        }

        string major2 = Attribute(Child(axis, "majorTickMarks"), "type") ?? "cross";
        string minor2 = Attribute(Child(axis, "minorTickMarks"), "type") ?? "cross";
        written.Add(Val("majorTickMark", major2), Val("minorTickMark", minor2), Val("tickLblPos", "nextTo"));

        if (Child(axis, "spPr") is { } shape) written.Add(new XElement(C("spPr"), shape.Nodes()));
        if (Child(axis, "txPr") is { } text) written.Add(new XElement(C("txPr"), text.Nodes()));

        written.Add(Val("crossAx", crosses), Val("crosses", "autoZero"));

        if (kind == "valAx" && Child(axis, "valScaling") is { } units)
        {
            if (Attribute(units, "majorUnit") is { } majorUnit) written.Add(Val("majorUnit", majorUnit));
            if (Attribute(units, "minorUnit") is { } minorUnit) written.Add(Val("minorUnit", minorUnit));
        }

        return written;
    }

    /// <summary>A gridline element's <c>cx:spPr</c>, as the <c>c:spPr</c> it stands for.</summary>
    private static XElement? Shape(XElement holder)
        => Child(holder, "spPr") is { } shape ? new XElement(C("spPr"), shape.Nodes()) : null;

    /// <summary>
    /// <c>cx:title</c>: a literal in <c>cx:tx/cx:txData/cx:v</c> and the formatting — and, in what
    /// Excel writes, the same text again as runs — in <c>cx:txPr</c>, which <c>TitleContext</c>
    /// reads exactly as it reads <c>c:tx</c>, <c>c:v</c> and <c>c:txPr</c>
    /// (<c>titlecontext.cxx</c>:48-120). <c>@pos</c> and <c>@align</c> are not read.
    /// </summary>
    private static XElement Title(XElement title)
    {
        XElement written = new(C("title"));

        if (Child(Child(title, "tx"), "txData") is { } txData
            && Child(txData, "v")?.Value is { Length: > 0 } text)
        {
            written.Add(new XElement(C("tx"), new XElement(C("v"), text)));
        }
        else if (Child(Child(title, "tx"), "rich") is { } rich)
        {
            written.Add(new XElement(C("tx"), new XElement(C("rich"), rich.Nodes())));
        }

        written.Add(Val("overlay", Attribute(title, "overlay") is "1" or "true" ? 1 : 0));
        if (Child(title, "spPr") is { } shape) written.Add(new XElement(C("spPr"), shape.Nodes()));
        if (Child(title, "txPr") is { } body) written.Add(new XElement(C("txPr"), body.Nodes()));

        return written;
    }

    /// <summary>
    /// <c>cx:dataLabels</c> as <c>c:dLbls</c>. Its <c>cx:visibility</c> is a TODO in the importer
    /// (<c>seriescontext.cxx</c>:84-85), so every show flag takes the non-2007 default, true, and
    /// a percentage only a pie would draw (<c>seriesconverter.cxx</c>:139-143).
    /// </summary>
    private static XElement DataLabels(XElement labels)
    {
        XElement written = new(C("dLbls"));

        if (Child(labels, "numFmt") is { } format)
        {
            written.Add(new XElement(C("numFmt"),
                new XAttribute("formatCode", Attribute(format, "formatCode") ?? "General"),
                new XAttribute("sourceLinked", Attribute(format, "sourceLinked") ?? "0")));
        }

        if (Child(labels, "spPr") is { } shape) written.Add(new XElement(C("spPr"), shape.Nodes()));
        if (Child(labels, "txPr") is { } text) written.Add(new XElement(C("txPr"), text.Nodes()));
        if (Attribute(labels, "pos") is { } position) written.Add(Val("dLblPos", position));

        written.Add(
            Val("showLegendKey", 1), Val("showVal", 1), Val("showCatName", 1),
            Val("showSerName", 1), Val("showPercent", 0), Val("showBubbleSize", 0));

        if (Child(labels, "separator")?.Value is { } separator)
            written.Add(new XElement(C("separator"), separator));

        return written;
    }

    /// <summary>
    /// <c>cx:binning</c>'s bin rule: a <c>cx:binSize</c> or <c>cx:binCount</c> child, read from
    /// <c>@val</c> or from its text (<c>BinningContext</c>, <c>seriescontext.cxx</c>:778-810).
    /// </summary>
    private static ChartBinning BinningOf(XElement series)
    {
        XElement? binning = Child(Child(series, "layoutPr"), "binning");

        if (Child(binning, "binSize") is { } size && NumberOf(size) is { } width)
            return new ChartBinning(ChartBinningMode.Width, Width: width);

        if (Child(binning, "binCount") is { } count && NumberOf(count) is { } bins)
        {
            return new ChartBinning(
                ChartBinningMode.Count, Count: (int)Math.Clamp(Math.Truncate(bins), 0, int.MaxValue));
        }

        return ChartBinning.Automatic;
    }

    private static double? NumberOf(XElement element)
    {
        string? text = Attribute(element, "val") ?? element.Value;
        return double.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double value)
            ? value
            : null;
    }

    private static XElement StringLiteral(IReadOnlyList<string?> points)
    {
        XElement literal = new(C("strLit"), Val("ptCount", points.Count));
        for (int at = 0; at < points.Count; at++)
        {
            if (points[at] is not { } text) continue;
            literal.Add(new XElement(C("pt"), new XAttribute("idx", at), new XElement(C("v"), text)));
        }

        return literal;
    }

    private static XElement NumberLiteral(IReadOnlyList<double?> points)
    {
        XElement literal = new(C("numLit"), Val("ptCount", points.Count));
        for (int at = 0; at < points.Count; at++)
        {
            if (points[at] is not { } number) continue;
            literal.Add(new XElement(C("pt"), new XAttribute("idx", at),
                new XElement(C("v"), number.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        }

        return literal;
    }

    private static XName C(string localName) => XName.Get(localName, OoxmlNamespaces.DrawingMLChart);

    private static XElement Val(string localName, object value)
        => new(C(localName), new XAttribute("val",
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""));

    /// <summary>One <c>cx:data</c> block: its category labels and its values.</summary>
    private readonly record struct Dimension(
        string?[] Categories, IReadOnlyList<double?> Values, string? Formula);

    private static Dictionary<string, Dimension> ReadData(
        XElement? chartData, ChartRangeResolver? ranges)
    {
        Dictionary<string, Dimension> map = [];
        if (chartData is null) return map;

        foreach (XElement block in Children(chartData, "data"))
        {
            if (Attribute(block, "id") is not { } id) continue;

            string?[] categories = [];
            List<double?> values = [];
            string? formula = null;

            foreach (XElement dimension in block.Elements())
            {
                if (dimension.Name.NamespaceName != OoxmlNamespaces.ExtendedChart) continue;

                bool numeric = dimension.Name.LocalName == "numDim";
                if (!numeric && dimension.Name.LocalName != "strDim") continue;

                Sequence sequence = ReadSequence(dimension, ranges);

                if (numeric && values.Count == 0)
                {
                    values = sequence.Numbers;
                    formula = Child(dimension, "f")?.Value;
                }
                else if (!numeric && categories.Length == 0) categories = [.. sequence.Text];
            }

            map[id] = new Dimension(categories, values, formula);
        }

        return map;
    }

    /// <summary>The cached points a dimension states, or what its formula resolves to.</summary>
    private static Sequence ReadSequence(XElement dimension, ChartRangeResolver? ranges)
    {
        List<string?> text = [];
        List<double?> numbers = [];

        // `cx:lvl/cx:pt idx="n"` — the cache. Sparse, so the index decides the slot.
        foreach (XElement level in Children(dimension, "lvl"))
        {
            foreach (XElement point in Children(level, "pt"))
            {
                if (Drawing.Number(point, "idx") is not { } index) continue;
                if (index is < 0 or >= MaximumPoints) continue;

                while (text.Count <= index) { text.Add(null); numbers.Add(null); }

                string value = point.Value;
                text[index] = value;

                if (double.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double number))
                {
                    numbers[index] = number;
                }
            }

            break;
        }

        if (text.Count > 0) return new Sequence(text, numbers);

        string? formula = Child(dimension, "f")?.Value;
        if (formula is null || ranges is null) return new Sequence(text, numbers);
        if (ranges(formula) is not { } resolved) return new Sequence(text, numbers);

        return new Sequence([.. resolved.Text], [.. resolved.Numbers]);
    }

    private readonly record struct Sequence(List<string?> Text, List<double?> Numbers);

    private static XName Name(string localName)
        => XName.Get(localName, OoxmlNamespaces.ExtendedChart);

    private static bool Is(XElement element, string localName)
        => element.Name == Name(localName);

    private static XElement? Child(XElement? element, string localName)
        => element?.Element(Name(localName));

    private static IEnumerable<XElement> Children(XElement? element, string localName)
        => element?.Elements(Name(localName)) ?? [];

    private static string? Attribute(XElement? element, string name)
        => element?.Attribute(name)?.Value;
}
