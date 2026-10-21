using System.Text;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// RTF's absolutely positioned paragraphs, placed where the 26.8 daily places them.
/// </summary>
/// <remarks>
/// Every expectation is a measurement of LibreOfficeDev 26.8.3.0.0 (9dcd8a837588) rendering the same file
/// under stock fontconfig: A4, one-inch margins, Liberation Serif 12 pt, a paragraph, the framed one, then
/// a paragraph of text. writerfilter turns the frame words into a <c>w:framePr</c>
/// (<c>RTFFrame::getSprms</c>, <c>rtfdocumentimpl.cxx</c>:4142-4235), so these exercise the translation
/// and leave the placement rules to <see cref="ParagraphFrameTests"/>.
/// </remarks>
public sealed class RtfParagraphFrameTests
{
    private const double Tolerance = 0.2;

    private const string Lorem =
        "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore "
        + "et dolore magna aliqua Ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut "
        + "aliquip ex ea commodo consequat Duis aute irure dolor in reprehenderit in voluptate velit esse "
        + "cillum dolore eu fugiat nulla pariatur Excepteur sint occaecat cupidatat non proident sunt in "
        + "culpa qui officia deserunt";

    private const string Frame = @"\absw2880\phcol\pvpara\posx1440\wraparound";

    /// <summary>A column- and paragraph-relative frame stands on the next paragraph's first line.</summary>
    [Fact]
    public void AParagraphRelativeFrameStandsBesideTheNextParagraph()
    {
        List<DrawnWord> words = Words(Frame);

        DrawnWord frame = words.Single(word => word.Text == "FRAMEA");
        frame.Left.ShouldBe(144.1, Tolerance);
        frame.Baseline.ShouldBe(97.0, Tolerance);
        words.Where(word => Math.Abs(word.Baseline - 97.0) < 0.5 && word.Left > 280)
            .Min(word => word.Left).ShouldBe(288.1, Tolerance);
    }

    /// <summary>
    /// With no <c>\pvpara</c> the frame is placed from the top margin, because the translation states
    /// an unstated vertical anchor as the margin — and <c>\posy</c> is one twip further down than it says.
    /// </summary>
    [Fact]
    public void AnUnanchoredOffsetIsMeasuredFromTheMargin()
    {
        DrawnWord frame = Words(Frame.Replace(@"\pvpara", string.Empty, StringComparison.Ordinal) + @"\posy2880")
            .Single(word => word.Text == "FRAMEA");

        frame.Baseline.ShouldBe(227.25, Tolerance);
    }

    /// <summary><c>\posy0</c> is not zero but one twip, so it is stated and the frame moves by it.</summary>
    [Fact]
    public void AZeroOffsetIsOneTwip()
    {
        DrawnWord frame = Words(Frame + @"\posy0").Single(word => word.Text == "FRAMEA");

        frame.Baseline.ShouldBe(97.05, 0.03);
    }

    /// <summary>
    /// A frame with no wrap word, and one with <c>\nowrap</c>, keep the text above and below them.
    /// </summary>
    [Theory]
    [InlineData(@"\nowrap")]
    [InlineData("")]
    public void AFrameWithoutAWrapWordKeepsTheTextAboveAndBelow(string wrap)
    {
        List<DrawnWord> words = Words(Frame.Replace(@"\wraparound", wrap, StringComparison.Ordinal));

        words.First(word => word.Text == "AFTER").Baseline.ShouldBe(124.6, Tolerance);
    }

    /// <summary><c>\wrapdefault</c> is <c>auto</c>, which writerfilter maps to the wider side only.</summary>
    [Fact]
    public void ADefaultWrapTakesTheWiderSide()
    {
        List<DrawnWord> words = Words(Frame.Replace(@"\wraparound", @"\wrapdefault", StringComparison.Ordinal));

        words.First(word => word.Text == "AFTER").Left.ShouldBe(288.1, Tolerance);
    }

    /// <summary>
    /// <c>\posxl</c> alone does not make a frame (<c>RTFFrame::hasProperties</c>, tdf#153178).
    /// </summary>
    [Fact]
    public void ALeftAlignmentAloneLeavesTheParagraphInTheFlow()
    {
        DrawnWord frame = Words(@"\posxl").Single(word => word.Text == "FRAMEA");

        frame.Left.ShouldBe(72.1, Tolerance);
        frame.Baseline.ShouldBe(97.0, Tolerance);
    }

    /// <summary>Consecutive paragraphs stating the same frame share it.</summary>
    [Fact]
    public void ConsecutiveEqualFramesAreOneFrame()
    {
        string rtf = Head + Par("BEFORE text") + Par("FRAMEA one two three four five six seven eight nine ten", Frame)
                     + Par("FRAMEB second framed paragraph", Frame) + Par("AFTER " + Lorem) + "}";

        DrawnWord second = Draw(rtf).Single(word => word.Text == "FRAMEB");

        second.Left.ShouldBe(144.1, Tolerance);
        second.Baseline.ShouldBe(124.6, Tolerance);
    }

    private const string Head =
        @"{\rtf1\ansi\deff0{\fonttbl{\f0\froman Liberation Serif;}}\paperw11906\paperh16838"
        + @"\margl1440\margr1440\margt1440\margb1440 ";

    private static string Par(string text, string properties = "")
        => @"\pard\plain" + properties + @"\f0\fs24 " + text + @"\par" + "\n";

    private static List<DrawnWord> Words(string frame)
        => Draw(Head + Par("BEFORE text") + Par("FRAMEA one two three four five six seven eight nine ten", frame)
                + Par("AFTER " + Lorem) + "}");

    private static List<DrawnWord> Draw(string rtf)
        => ParagraphFrameTests.Draw(Encoding.ASCII.GetBytes(rtf), "probe.rtf");
}
