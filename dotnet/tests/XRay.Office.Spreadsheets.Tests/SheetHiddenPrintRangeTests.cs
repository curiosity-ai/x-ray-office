using XRay.Office.Core.Documents;
using XRay.Office.Spreadsheets.Layout;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Spreadsheets.Tests;

/// <summary>
/// A hidden sheet that states a print range of its own is printed.
/// </summary>
/// <remarks>
/// <c>ScPrintFunc::InitParam</c> (<c>sc/source/ui/view/printfun.cxx</c>:1018-1048, the same in
/// 26.8.1.1) asks about visibility only in the arm for a sheet with no print range.
/// <c>features/sheet-hidden-print-area.xlsx</c> is a visible sheet and a hidden one whose
/// <c>Print_Area</c> is A1:B2: 26.8.1.1 prints two pages, the second holding
/// <c>HIDDENSHEETTEXT</c> at the visible sheet's own text origin, and this tree printed one.
/// Master's 8bca7a7c3f6e stops printing it; 26.8.1.1 does not have that commit.
/// </remarks>
public sealed class SheetHiddenPrintRangeTests
{
    [Fact]
    public void AHiddenSheetWithItsOwnPrintRangeIsPrinted()
    {
        using IPaginatedDocument document =
            (IPaginatedDocument)OfficeDocument.Open(Corpus.Require("sheet-hidden-print-area.xlsx"));
        SpreadsheetPages pages = (SpreadsheetPages)document.Layout();

        pages.Count.ShouldBe(2);
        pages.Pages[1].Sheet.IsHidden.ShouldBeTrue();
    }

    [Theory]
    // No sheet states a range: only visibility decides.
    [InlineData(false, false, true, false, true)]
    [InlineData(true, false, true, false, false)]
    // Some sheet does: a sheet stating its own prints whether or not it is hidden...
    [InlineData(false, true, true, true, true)]
    [InlineData(true, true, true, true, true)]
    // ...and one stating none prints for its entire-sheet flag, and only when visible.
    [InlineData(false, false, true, true, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, false, false, true, false)]
    public void WhichSheetsPrintFollowsInitParam(
        bool hidden, bool ownRange, bool entireSheet, bool anyRange, bool expected)
    {
        SheetLayout sheet = new()
        {
            Name = "S",
            IsHidden = hidden,
            Setup = new SheetPrintSetup
            {
                PrintAreas = ownRange ? [new SheetRange(0, 0, 1, 1)] : [],
                PrintsEntireSheet = entireSheet,
            },
        };

        SpreadsheetPages.IsPrinted(sheet, anyRange).ShouldBe(expected);
    }
}
