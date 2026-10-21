using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// Where a dropped capital stands in a centred or right-set paragraph: leading the first line's text,
/// pulled left as far as the drop's other lines reach (<c>SwTextAdjuster::CalcDropAdjust</c>).
/// </summary>
/// <remarks>
/// Figures are the 26.8 daily's, a <c>W</c> dropped over 12 pt Liberation Serif on an A4 page with
/// 1 in margins (<c>dotnet/probes/drop-adjust/</c>), less the two writers' 0.10 pt text-origin offset.
/// </remarks>
public sealed class DropCapAlignmentTests
{
    private const string Lorem =
        "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et "
        + "dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip "
        + "ex ea commodo consequat duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu "
        + "fugiat nulla pariatur.";

    /// <summary>The letter leads the line, moved left to the furthest-left of its other lines.</summary>
    [Theory]
    [InlineData(null, 3, 72.0)]
    [InlineData("center", 3, 72.90)]
    [InlineData("center", 2, 83.60)]
    [InlineData("right", 3, 73.85)]
    public void TheLetterLeadsAnAlignedFirstLine(string? alignment, int lines, double left)
    {
        string jc = alignment is null ? "" : $"<w:jc w:val=\"{alignment}\"/>";
        List<DrawnWord> words = ParagraphFrameTests.Words(
            P("BEFORE text")
            + P("W", $"<w:framePr w:dropCap=\"drop\" w:lines=\"{lines}\" w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"text\"/>{jc}")
            + P("AFTER " + Lorem, jc));

        words.Single(word => word.Text == "W").Left.ShouldBe(left, 0.2);
    }

    private static string P(string text, string properties = "")
        => $"<w:p><w:pPr>{properties}</w:pPr><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";
}
