using System.Text;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// Raised and lowered text: where it is drawn, how it grows its line, and the three spellings of a
/// half-point offset — DOCX <c>w:position</c>, RTF <c>\up</c>/<c>\dn</c> — beside the automatic keywords.
/// </summary>
/// <remarks>
/// <para>
/// Every figure is the 26.8 daily's, on Liberation Serif 12 pt (a 13.8 pt pitch) unless the case says
/// otherwise (<c>dotnet/probes/escapement-r1/</c>). Three rules are pinned:
/// </para>
/// <list type="bullet">
///   <item>
///     A numeric escapement raises the run by its percentage of the unshrunk height and grows the line by
///     the same: <c>SwSubFont::CalcEscAscent</c>/<c>CalcEscHeight</c>, <c>swfont.cxx</c>:495-506, :965-978.
///   </item>
///   <item>
///     An automatic one (<c>w:vertAlign</c>, <c>\super</c>, ODF's <c>super</c>) is placed by the two fonts'
///     ascents — or descents, lowered — and grows nothing: <c>SwSubFont::CalcEsc</c>, :1417-1450. So
///     <c>super 100%</c> does not move at all, and a subscript is lowered far less than a third of the height.
///   </item>
///   <item>
///     <c>w:position</c> is <c>round(halves / 2 / size × 100)</c> per cent at full size, deferred so that it
///     beats a <c>w:vertAlign</c> beside it; a character style's, and a paragraph style's positive one, are
///     the automatic pair instead (<c>DomainMapper.cxx</c>:2434-2464, :3997-4019).
///   </item>
/// </list>
/// </remarks>
public sealed class EscapedLineHeightTests
{
    /// <summary>
    /// <c>w:position</c> at 3, 6 and 12 half-points raises the run 1.75, 3.45 and 6.90 pt, and the line by as much.
    /// </summary>
    [Theory]
    [InlineData(3, 1.75)]
    [InlineData(6, 3.45)]
    [InlineData(12, 6.90)]
    [InlineData(-6, -3.45)]
    public void APositionRaisesItsRunAndGrowsItsLine(int halves, double rise)
    {
        Lines lines = Docx($"<w:position w:val=\"{halves}\"/>");
        lines.Rise.ShouldBe(rise, 0.01);
        lines.Pitch.ShouldBe(13.8 + Math.Abs(rise), 0.01);
    }

    /// <summary>The percentage is of the run's own size: 6 half-points at 18 pt is 17 %, 3.50 pt.</summary>
    [Fact]
    public void APositionIsAPercentageOfTheRunsOwnSize()
        => Docx("<w:position w:val=\"6\"/><w:sz w:val=\"36\"/>").Rise.ShouldBe(3.50, 0.01);

    /// <summary>A run's own <c>w:position</c> beats its <c>w:vertAlign</c>, and keeps the full size.</summary>
    [Fact]
    public void APositionBeatsAVerticalAlignmentBesideIt()
        => Docx("<w:vertAlign w:val=\"superscript\"/><w:position w:val=\"6\"/>").Rise.ShouldBe(3.45, 0.01);

    /// <summary>
    /// An automatic superscript is raised by the two ascents' difference, 4.70 pt here, and grows nothing.
    /// </summary>
    /// <remarks>A third of the height, the rule this replaced, is 4.55.</remarks>
    [Fact]
    public void AnAutomaticSuperscriptIsPlacedByTheAscents()
    {
        Lines lines = Docx("<w:vertAlign w:val=\"superscript\"/>");
        lines.Rise.ShouldBe(4.70, 0.01);
        lines.Pitch.ShouldBe(13.8, 0.01);
    }

    /// <summary>A character style's <c>w:position</c> is the automatic superscript, not its offset.</summary>
    [Fact]
    public void ACharacterStylesPositionIsTheAutomaticPair()
        => Docx(
                "<w:rStyle w:val=\"Up\"/>",
                "<w:style w:type=\"character\" w:styleId=\"Up\"><w:name w:val=\"Up\"/>"
                + "<w:rPr><w:position w:val=\"6\"/></w:rPr></w:style>")
            .Rise.ShouldBe(4.70, 0.01);

    /// <summary><c>\up6</c> is <c>w:position</c>'s sprm, and <c>\dn12</c> lowers by half the height.</summary>
    [Theory]
    [InlineData(@"\up6", 3.45)]
    [InlineData(@"\dn12", -6.90)]
    [InlineData(@"\super\up6", 3.45)]
    public void AnRtfOffsetIsAPosition(string words, double rise)
    {
        Lines lines = Rtf(words);
        lines.Rise.ShouldBe(rise, 0.01);
        lines.Pitch.ShouldBe(13.8 + Math.Abs(rise), 0.01);
    }

    /// <summary>
    /// An automatic subscript is lowered by the two descents' difference: 1.30 pt at 12 pt Carlito.
    /// </summary>
    /// <remarks>A third of the height would lower it 4.80.</remarks>
    [Fact]
    public void AnAutomaticSubscriptIsPlacedByTheDescents()
        => Rtf(@"\sub", "Carlito").Rise.ShouldBe(-1.30, 0.01);

    /// <summary>ODF's <c>super 100%</c> is automatic at full size, so it does not move.</summary>
    [Theory]
    [InlineData("super 100%", 0.0, 13.8)]
    [InlineData("50% 100%", 6.90, 20.7)]
    [InlineData("-20% 100%", -2.75, 16.55)]
    public void AnOdfPositionFollowsTheSameTwoRules(string position, double rise, double pitch)
    {
        Lines lines = Odf(position);
        lines.Rise.ShouldBe(rise, 0.01);
        lines.Pitch.ShouldBe(pitch, 0.01);
    }

    private static Lines Docx(string runProperties, string styles = "")
        => Lines.Of(ParagraphFrameTests.Words(
            "<w:p><w:r><w:t>Alpha</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t xml:space=\"preserve\">Base </w:t></w:r>"
            + $"<w:r><w:rPr>{runProperties}</w:rPr><w:t>MARK</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t>Charlie</w:t></w:r></w:p>",
            styles));

    private static Lines Rtf(string words, string face = "Liberation Serif")
        => Lines.Of(ParagraphFrameTests.Draw(
            Encoding.ASCII.GetBytes(
                @"{\rtf1\ansi\deff0{\fonttbl{\f0 " + face + @";}}\paperw12240\paperh15840\margl1440\margr1440"
                + @"\margt1440\margb1440 \pard\plain\fs24 Alpha\par\pard\plain\fs24 Base {" + words
                + @" MARK}\par\pard\plain\fs24 Charlie\par}"),
            "escapement.rtf"));

    private static Lines Odf(string position)
        => Lines.Of(ParagraphFrameTests.Draw(
            Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document "
                + "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" "
                + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" "
                + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
                + "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" "
                + "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.3\" "
                + "office:mimetype=\"application/vnd.oasis.opendocument.text\">"
                + "<office:font-face-decls><style:font-face style:name=\"F\" svg:font-family=\"'Liberation Serif'\"/>"
                + "</office:font-face-decls><office:styles><style:default-style style:family=\"paragraph\">"
                + "<style:paragraph-properties fo:margin-top=\"0in\" fo:margin-bottom=\"0in\"/>"
                + "<style:text-properties style:font-name=\"F\" fo:font-size=\"12pt\"/></style:default-style>"
                + "</office:styles><office:automatic-styles><style:style style:name=\"T1\" style:family=\"text\">"
                + $"<style:text-properties style:text-position=\"{position}\"/></style:style>"
                + "</office:automatic-styles><office:body><office:text><text:p>Alpha</text:p>"
                + "<text:p>Base <text:span text:style-name=\"T1\">MARK</text:span></text:p><text:p>Charlie</text:p>"
                + "</office:text></office:body></office:document>"),
            "escapement.fodt"));

    /// <summary>The escaped word's rise over its line's baseline, and the pitch the line takes.</summary>
    private sealed record Lines(double Rise, double Pitch)
    {
        public static Lines Of(List<DrawnWord> words)
        {
            DrawnWord alpha = words.Single(word => word.Text == "Alpha");
            DrawnWord @base = words.Single(word => word.Text == "Base");
            DrawnWord mark = words.Single(word => word.Text == "MARK");
            DrawnWord charlie = words.Single(word => word.Text == "Charlie");

            // Down the page: the pitch above the escaped line plus the one below it, less one plain pitch,
            // is the escaped line's own height.
            double height = (@base.Baseline - alpha.Baseline) + (charlie.Baseline - @base.Baseline) - 13.8;
            return new Lines(@base.Baseline - mark.Baseline, height);
        }
    }
}
