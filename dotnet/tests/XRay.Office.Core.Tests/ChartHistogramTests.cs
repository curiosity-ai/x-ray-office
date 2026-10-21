using XRay.Office.Core.Charts;
using Shouldly;

namespace XRay.Office.Core.Tests;

/// <summary>
/// The bins a chartex histogram draws, against what the 26.8 daily drew for the same values.
/// </summary>
/// <remarks>
/// Every expected label here was read out of the daily's PDF of an authored workbook — one column
/// of values, <c>SimpleHistogram.xlsx</c>'s chart with only <c>cx:binning</c> varied.
/// </remarks>
public class ChartHistogramTests
{
    private static readonly double?[] Simple = [12, 10, 11, 13, 15];

    private static readonly double?[] Twenty =
    [
        46.9, 56.1, 47.3, 46.2, 38.8, 47.4, 63.3, 55.1, 62.4, 53.0,
        54.7, 52.2, 30.0, 60.3, 56.1, 56.0, 29.7, 29.1, 39.3, 44.4,
    ];

    /// <summary>Scott's rule on <c>SimpleHistogram.xlsx</c>'s five values.</summary>
    [Fact]
    public void ScottsRuleGivesTheQaDocumentsTwoBins()
    {
        ChartHistogramBins bins = ChartHistogram.Compute(Simple, ChartBinning.Automatic);

        bins.Categories.ShouldBe(["[10-13.94]", "(13.94-17.87]"]);
        bins.Frequencies.ShouldBe([4.0, 1.0]);
    }

    /// <summary>
    /// A fixed bin count decides the bars and not the labels: four bars under Scott's three.
    /// </summary>
    [Fact]
    public void ABinCountDecidesTheBarsAndScottDecidesTheLabels()
    {
        ChartHistogramBins bins = ChartHistogram.Compute(
            Twenty, new ChartBinning(ChartBinningMode.Count, Count: 4));

        bins.Categories.ShouldBe(["[29.1-42.7]", "(42.7-56.3]", "(56.3-69.9]", null]);
        bins.Frequencies.Count.ShouldBe(4);
        bins.Frequencies.Sum(f => f ?? 0).ShouldBe(20);
    }

    /// <summary>A fixed width gives ceil(range / width) bars — seven for 5 over 34.2.</summary>
    [Fact]
    public void ABinWidthGivesCeilingOfTheRangeOverTheWidthBars()
    {
        ChartHistogramBins bins = ChartHistogram.Compute(
            Twenty, new ChartBinning(ChartBinningMode.Width, Width: 5));

        bins.Frequencies.ShouldBe([3.0, 1.0, 1.0, 5.0, 2.0, 5.0, 3.0]);
        bins.Categories.Skip(3).ShouldAllBe(label => label == null);
    }

    /// <summary>One bin count keeps only the first label, centred under one bar.</summary>
    [Fact]
    public void OneBinKeepsOnlyTheFirstLabel()
    {
        ChartHistogramBins bins = ChartHistogram.Compute(
            Twenty, new ChartBinning(ChartBinningMode.Count, Count: 1));

        bins.Categories.ShouldBe(["[29.1-42.7]"]);
        bins.Frequencies.ShouldBe([20.0]);
    }

    /// <summary>A non-positive fixed value is the automatic rule, as the calculator falls back.</summary>
    [Fact]
    public void AZeroBinCountIsAutomatic()
        => ChartHistogram.Compute(Twenty, new ChartBinning(ChartBinningMode.Count, Count: 0))
            .Categories.Count.ShouldBe(3);

    /// <summary>One value, or none spread, is one bin from its floor to the ceiling past it.</summary>
    [Fact]
    public void NoSpreadIsOneBinFromTheFloorToOnePastTheCeiling()
    {
        ChartHistogram.Compute([3.5], ChartBinning.Automatic).Categories.ShouldBe(["[3-5]"]);
        ChartHistogram.Compute([7, 7, 7, 7], ChartBinning.Automatic).Categories.ShouldBe(["[7-8]"]);
    }

    /// <summary>
    /// The decimals follow the first bin's width: one for a width of 28, five for one of 0.005.
    /// </summary>
    [Fact]
    public void TheDecimalsFollowTheFirstBinsWidth()
    {
        ChartHistogram.Compute([45, 32, 18, 9, 25], ChartBinning.Automatic).Categories
            .ShouldBe(["[9-37]", "(37-65.1]"]);
        ChartHistogram.Compute(
                [0.0012, 0.0035, 0.0041, 0.0057, 0.0063, 0.0078, 0.0091, 0.0022],
                ChartBinning.Automatic)
            .Categories.ShouldBe(["[0.0012-0.00598]", "(0.00598-0.01075]"]);
        ChartHistogram.Compute([-5.5, -3.2, -1, 0, 1.7, 2.2, 4.9, -2.6, 3.3], ChartBinning.Automatic)
            .Categories.ShouldBe(["[-5.5-0.17]", "(0.17-5.84]"]);
    }

    /// <summary>A cell with no number is not a value.</summary>
    [Fact]
    public void AnEmptyCellIsSkipped()
        => ChartHistogram.Compute([12, null, 10, 11, 13, 15], ChartBinning.Automatic)
            .Frequencies.ShouldBe([4.0, 1.0]);

    /// <summary><c>OUString::number</c>'s G-17 form.</summary>
    [Theory]
    [InlineData(13.94, "13.94")]
    [InlineData(37.0, "37")]
    [InlineData(1200.0, "1200")]
    [InlineData(0.00598, "0.00598")]
    [InlineData(0.00001, "1E-005")]
    [InlineData(-5.5, "-5.5")]
    [InlineData(1e17, "1E+017")]
    [InlineData(0.1 + 0.2, "0.3")]
    public void BoundariesAreWrittenAsOUStringNumberWritesThem(double value, string expected)
        => ChartHistogram.Number(value).ShouldBe(expected);
}
