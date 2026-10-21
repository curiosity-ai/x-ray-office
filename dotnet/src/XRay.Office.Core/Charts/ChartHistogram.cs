using System.Globalization;
using System.Text;

namespace XRay.Office.Core.Charts;

/// <summary>How a histogram's bins are chosen.</summary>
/// <remarks>
/// <c>HistogramChartType</c>'s <c>FrequencyType</c> property: 0 automatic, 1 a fixed bin width,
/// 2 a fixed bin count (<c>chart2/source/inc/HistogramCalculator.hxx</c>, 26-8 branch). A fixed
/// mode with a value that is not positive falls back to automatic, exactly as the calculator does.
/// </remarks>
public enum ChartBinningMode
{
    /// <summary>Scott's rule — <c>cx:binning</c> with neither <c>cx:binSize</c> nor <c>cx:binCount</c>.</summary>
    Automatic = 0,

    /// <summary><c>cx:binSize</c>: every bin is this wide.</summary>
    Width,

    /// <summary><c>cx:binCount</c>: the range is cut into this many equal bins.</summary>
    Count,
}

/// <summary>A chartex <c>cx:binning</c>, as far as the 26.8 reference reads it.</summary>
/// <param name="Mode">Which of the three binning rules applies.</param>
/// <param name="Width">The bin width, for <see cref="ChartBinningMode.Width"/>.</param>
/// <param name="Count">The bin count, for <see cref="ChartBinningMode.Count"/>.</param>
/// <remarks>
/// <c>intervalClosed</c>, <c>underflow</c> and <c>overflow</c> are deliberately absent. The 26-8
/// branch's <c>BinningContext</c> stores <c>intervalClosed</c> as a series property nothing in the
/// view reads, and leaves the other two as a TODO
/// (<c>oox/source/drawingml/chart/seriescontext.cxx</c>:854-861); master's calculator grew both
/// later. Measured on the 26.8 daily: <c>intervalClosed="l"</c> and <c>"r"</c> draw byte-identical
/// bars, and <c>underflow="40" overflow="60"</c> draws the same three bins as no attribute at all.
/// </remarks>
public readonly record struct ChartBinning(ChartBinningMode Mode, double Width = 0.0, int Count = 0)
{
    /// <summary>Scott's rule, which is what a <c>cx:binning</c> with no child means.</summary>
    public static ChartBinning Automatic => new(ChartBinningMode.Automatic);
}

/// <summary>
/// The bins a chartex histogram draws: one category label and one frequency per slot.
/// </summary>
/// <param name="Categories">
/// The label under each slot, or null where the reference draws none — see
/// <see cref="ChartHistogram.Compute"/> for why a slot may have no label.
/// </param>
/// <param name="Frequencies">How many values fall into each slot.</param>
public sealed record ChartHistogramBins(
    IReadOnlyList<string?> Categories, IReadOnlyList<double?> Frequencies);

/// <summary>
/// A port of LibreOffice's histogram binning — <c>HistogramCalculator</c> and the label text
/// <c>HistogramDataSequence</c> writes for it — as the 26.8 branch draws it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The bars and the labels come from two different binnings, and that is the
/// reference's behaviour rather than a simplification.</strong> The frequencies are computed by
/// <c>HistogramChartType::createCalculatedDataSeries</c>, which reads the chart type's
/// <c>FrequencyType</c>, <c>BinWidth</c> and <c>BinCount</c>
/// (<c>chart2/source/model/template/HistogramChartType.cxx</c>:159-216, 26-8 branch). The
/// categories the axis draws are the ones <c>HistogramDataInterpreter::interpretDataSource</c>
/// built when the template was applied, with <c>new HistogramDataSequence(valuesYSeqData, true)</c>
/// and therefore the automatic rule (<c>HistogramDataInterpreter.cxx</c>:62-65), and
/// <c>AxisConverter</c> keeps those (<c>axisconverter.cxx</c>:160-170, 233-236). Measured on the
/// 26.8 daily over one twenty-value column: <c>cx:binCount val="4"</c> draws four bars under the
/// three automatic labels, <c>cx:binSize val="5"</c> draws seven bars under the same three, and
/// <c>cx:binCount val="1"</c> draws one bar under the first of them. So the slots follow the
/// frequencies and a slot past the last automatic bin has no label.
/// </para>
/// <para>
/// The ODF export says the same thing from the model's side: <c>loext:frequency-type="2"</c>
/// <c>loext:bin-count="4"</c> is written out for the four-bar chart whose labels are Scott's.
/// </para>
/// </remarks>
public static class ChartHistogram
{
    /// <summary>The most bins a fixed width is allowed to produce.</summary>
    /// <remarks>
    /// The calculator has no ceiling; a hostile <c>cx:binSize</c> of a millionth over a range of
    /// a million would allocate a trillion bins. The cap is far above anything a chart can show.
    /// </remarks>
    private const int MaximumBins = 65536;

    /// <summary>The slots a histogram draws over <paramref name="values"/>.</summary>
    /// <param name="values">The series' raw values; a null is a cell with no number and is skipped.</param>
    /// <param name="binning">The file's <c>cx:binning</c>, which decides the frequencies only.</param>
    public static ChartHistogramBins Compute(IReadOnlyList<double?> values, ChartBinning binning)
    {
        ArgumentNullException.ThrowIfNull(values);

        List<double> data = [];
        foreach (double? value in values)
            if (value is { } number) data.Add(number);

        if (data.Count == 0) return new ChartHistogramBins([], []);

        (List<(double Start, double End)> automatic, _) = Bins(data, ChartBinning.Automatic);
        (List<(double Start, double End)> ranges, int[] frequencies) = Bins(data, binning);

        string[] labels = Labels(automatic);

        string?[] categories = new string?[ranges.Count];
        double?[] heights = new double?[ranges.Count];

        for (int at = 0; at < ranges.Count; at++)
        {
            categories[at] = at < labels.Length ? labels[at] : null;
            heights[at] = frequencies[at];
        }

        return new ChartHistogramBins(categories, heights);
    }

    /// <summary>
    /// <c>HistogramCalculator::computeBinFrequencyHistogram</c>, 26-8 branch: the bin ranges and
    /// the count in each.
    /// </summary>
    /// <remarks>
    /// The first bin is closed at both ends and every later one is open below, which is the only
    /// interval rule the branch has. Statistics skip a value that is not finite, as the calculator
    /// does; the values here are already finite or absent.
    /// </remarks>
    internal static (List<(double Start, double End)> Ranges, int[] Frequencies) Bins(
        IReadOnlyList<double> data, ChartBinning binning)
    {
        double sum = 0.0;
        double squares = 0.0;
        double minimum = data[0];
        double maximum = data[0];
        int count = 0;

        foreach (double value in data)
        {
            if (!double.IsFinite(value)) continue;

            sum += value;
            squares += value * value;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            count++;
        }

        // HistogramCalculator.cxx:51-58 — fewer than two points, or no spread, is one bin from the
        // floor of the value to the ceiling of one past it.
        if (count < 2 || minimum == maximum)
            return ([(Math.Floor(minimum), Math.Ceiling(minimum + 1.0))], [count]);

        int bins = 0;
        double width = 0.0;
        bool resolved = false;

        if (binning.Mode == ChartBinningMode.Width && binning.Width > 0.0)
        {
            width = binning.Width;
            bins = CeilingToInt((maximum - minimum) / width);
            resolved = true;
        }
        else if (binning.Mode == ChartBinningMode.Count && binning.Count > 0)
        {
            bins = binning.Count;
            width = (maximum - minimum) / bins;
            resolved = true;
        }

        if (!resolved)
        {
            // Scott's rule: 3.5 standard deviations over the cube root of the count.
            double mean = sum / count;
            double variance = (squares - (sum * mean)) / (count - 1);
            width = 3.5 * Math.Sqrt(variance) / Math.Cbrt(count);
            bins = CeilingToInt((maximum - minimum) / width);
        }

        bins = Math.Clamp(bins, 1, MaximumBins);

        List<(double Start, double End)> ranges = new(bins);
        double start = minimum;

        for (int at = 0; at < bins; at++)
        {
            double end = start + width;
            ranges.Add((at == 0 ? minimum : start, end));
            start = end;
        }

        // The last bin is stretched to take the maximum whatever rounding did to the widths.
        ranges[^1] = (ranges[^1].Start, Math.Max(ranges[^1].End, maximum));

        int[] frequencies = new int[bins];

        foreach (double value in data)
        {
            if (!double.IsFinite(value)) continue;

            for (int at = 0; at < ranges.Count; at++)
            {
                bool inside = at == 0
                    ? value >= ranges[at].Start && value <= ranges[at].End
                    : value > ranges[at].Start && value <= ranges[at].End;

                if (!inside) continue;

                frequencies[at]++;
                break;
            }
        }

        return (ranges, frequencies);
    }

    /// <summary>
    /// The bin labels, <c>[a-b]</c> for the first and <c>(a-b]</c> for every other —
    /// <c>HistogramDataSequence::ensureCalculated</c>.
    /// </summary>
    /// <remarks>
    /// The boundaries keep about three significant figures relative to the first bin's width:
    /// <c>max(1, 2 - floor(log10(width)))</c> decimals, rounded half away from zero, then written
    /// by <c>OUString::number</c> — so a width near 1 gets two decimals and one near 0.002 gets
    /// five (<c>chart2/source/tools/HistogramDataSequence.cxx</c>:222-247, 26-8 branch).
    /// </remarks>
    internal static string[] Labels(IReadOnlyList<(double Start, double End)> ranges)
    {
        int digits = 2;
        if (ranges.Count > 0)
        {
            double first = ranges[0].End - ranges[0].Start;
            if (first > 0.0) digits = Math.Max(1, 2 - (int)Math.Floor(Math.Log10(first)));
        }

        double scale = Math.Pow(10.0, digits);
        string Boundary(double value)
            => Number(Math.Round(value * scale, MidpointRounding.AwayFromZero) / scale);

        string[] labels = new string[ranges.Count];
        for (int at = 0; at < ranges.Count; at++)
        {
            labels[at] = (at == 0 ? "[" : "(") + Boundary(ranges[at].Start) + "-"
                         + Boundary(ranges[at].End) + "]";
        }

        return labels;
    }

    /// <summary>
    /// <c>OUString::number(double)</c>: the shortest round-trip digits, rounded to fifteen
    /// significant ones, written fixed unless the exponent is below -4 or at least 17.
    /// </summary>
    /// <remarks>
    /// <c>rtl::str::valueOfFP</c> calls <c>doubleToString</c> with
    /// <c>rtl_math_StringFormat_G</c>, 17 places and trailing zeros erased
    /// (<c>sal/rtl/strtmpl.hxx</c>:1720-1731, 1395-1716). Dragonbox gives the shortest digits;
    /// the G branch rounds them to <c>min(17, 15)</c> significant digits half-up and switches to
    /// <c>E</c> with a signed three-digit exponent outside that window.
    /// </remarks>
    public static string Number(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value < 0 ? "-INF" : "INF";

        bool negative = double.IsNegative(value);
        if (negative) value = -value;

        if (value == 0.0) return negative ? "-0" : "0";

        // The shortest round-trip form, which .NET writes as digits with an optional point and
        // an optional exponent; reduce it to a digit string and the exponent of its first digit.
        string shortest = value.ToString("R", CultureInfo.InvariantCulture);
        int e = shortest.IndexOfAny(['E', 'e']);
        int exponent = e >= 0 ? int.Parse(shortest[(e + 1)..], CultureInfo.InvariantCulture) : 0;
        string mantissa = e >= 0 ? shortest[..e] : shortest;

        int point = mantissa.IndexOf('.');
        string whole = point >= 0 ? mantissa[..point] : mantissa;
        string fraction = point >= 0 ? mantissa[(point + 1)..] : "";
        string digits = (whole + fraction).TrimStart('0');
        int leadingZeros = (whole + fraction).Length - (whole + fraction).TrimStart('0').Length;
        int nExp = exponent + whole.Length - 1 - leadingZeros;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0) digits = "0";

        if (digits.Length > 15)
        {
            // roundToPow10: half-up on the decimal digits that are dropped.
            bool up = digits[15] >= '5';
            char[] kept = digits[..15].ToCharArray();
            int at = 14;
            while (up && at >= 0)
            {
                if (kept[at] == '9')
                {
                    kept[at] = '0';
                    at--;
                }
                else
                {
                    kept[at]++;
                    up = false;
                }
            }

            digits = new string(kept).TrimEnd('0');
            if (up)
            {
                digits = "1";
                nExp++;
            }

            if (digits.Length == 0) digits = "0";
        }

        StringBuilder text = new();
        if (negative) text.Append('-');

        if (nExp < -4 || nExp >= 17)
        {
            text.Append(digits[0]);
            if (digits.Length > 1) text.Append('.').Append(digits, 1, digits.Length - 1);

            text.Append('E').Append(nExp < 0 ? '-' : '+');
            text.Append(Math.Abs(nExp).ToString("000", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        if (nExp < 0)
        {
            text.Append("0.").Append('0', -nExp - 1).Append(digits);
            return text.ToString();
        }

        if (digits.Length <= nExp + 1)
        {
            text.Append(digits).Append('0', nExp + 1 - digits.Length);
            return text.ToString();
        }

        text.Append(digits, 0, nExp + 1).Append('.').Append(digits, nExp + 1, digits.Length - nExp - 1);
        return text.ToString();
    }

    private static int CeilingToInt(double value)
    {
        double ceiling = Math.Ceiling(value);
        if (double.IsNaN(ceiling) || ceiling < 1.0) return 1;
        return ceiling > MaximumBins ? MaximumBins : (int)ceiling;
    }
}
