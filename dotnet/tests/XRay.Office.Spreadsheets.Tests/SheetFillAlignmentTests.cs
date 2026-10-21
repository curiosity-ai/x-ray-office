using XRay.Office.Core.Documents;
using XRay.Office.Spreadsheets.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Spreadsheets.Tests;

/// <summary>
/// A fill-aligned cell (<c>horizontal="fill"</c>) repeats its text across its column.
/// </summary>
/// <remarks>
/// <para>
/// <c>ScOutputData::LayoutStrings</c> (<c>sc/source/ui/view/output2.cxx</c>:1895-1914 in
/// 26.8.1.1) repeats the whole string as many whole times as the cell holds, when the text fits
/// the cell at all; X-Ray.Office drew it once. <c>features/sheet-fill-alignment.xlsx</c>
/// (<c>probes/calc-anchor-r26/make-fill.py</c>) holds five rows over columns 5, 12 and 20
/// characters wide. Every expectation is 26.8.1.1's own text layer for the file; the positions
/// agree to 0.1 pt.
/// </para>
/// <para>
/// Row 4 does not fit its cells and is not repeated — it is shortened like any other text. Row
/// 5 states a line break as well, which a plain number never takes (tdf#121040), so it repeats
/// and stays at the left.
/// </para>
/// </remarks>
public sealed class SheetFillAlignmentTests
{
    private static List<string> Texts()
    {
        using IPaginatedDocument document =
            (IPaginatedDocument)OfficeDocument.Open(Corpus.Require("sheet-fill-alignment.xlsx"));

        RecordingDrawingSink sink = new();
        ((SpreadsheetPages)document.Layout()).Pages[0].Draw(sink);

        return [.. sink.Pages[0].Runs
            .OrderBy(run => Math.Round(run.Origin.Y.Points))
            .ThenBy(run => run.Origin.X.Points)
            .Select(run => run.Text.Trim())
            .Where(text => text.Length > 0)];
    }

    [Fact]
    public void EachCellRepeatsItsTextAsManyTimesAsItsColumnHolds()
    {
        List<string> texts = Texts();

        texts.ShouldContain(new string('-', 7));
        texts.ShouldContain(new string('-', 19));
        texts.ShouldContain(new string('-', 32));
        texts.ShouldContain("abab");
        texts.ShouldContain(string.Concat(Enumerable.Repeat("ab", 5)));
        texts.ShouldContain(string.Concat(Enumerable.Repeat("ab", 9)));
        texts.Count(text => text == new string('7', 4)).ShouldBe(2, "rows 3 and 5");
        texts.Count(text => text == new string('7', 11)).ShouldBe(2);
        texts.Count(text => text == new string('7', 19)).ShouldBe(2);
    }

    [Fact]
    public void TextWiderThanItsCellIsNotRepeated()
        => Texts().ShouldNotContain(text => text.Contains("pleaseFill", StringComparison.Ordinal));
}
