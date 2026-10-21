using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using XRay.Office.TestKit;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// An RTF paragraph's borders and shading: <c>\brdrt</c> and its four siblings, <c>\box</c>,
/// <c>\brdrbtw</c>, the describing words, and <c>\cbpat</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every figure is the 26.8 daily's, measured on one-attribute probes rendered by both
/// (<c>dotnet/probes/rtf-borders/</c>), and every one differs from what the DOCX path's model would give
/// for the same numbers in at least one of three ways: the box sits inside the indents and narrows the
/// text, a between rule becomes the second paragraph's top with the first's bottom <em>distance</em> kept,
/// and Writer joins two boxes on the upper paragraph's connect flag and on lines only.
/// </para>
/// <para>
/// The body is Liberation Serif 12 pt, whose plain pitch is 13.8 pt, on a page with 1 inch margins.
/// </para>
/// </remarks>
public sealed class RtfParagraphBorderTests
{
    private const double Pitch = 13.8;

    /// <summary>
    /// A 10 twip top rule with 80 twips of spacing takes 0.5 + 4 pt above its paragraph.
    /// </summary>
    /// <remarks><c>\brdrw</c> is twips and <c>\brsp</c> whole points of twentieths (reference 694.50 → 690.50).</remarks>
    [Fact]
    public void ATopRuleTakesItsWidthAndItsSpacing()
    {
        Layout pages = Lay(@"\pard\brdrt\brdrs\brdrw10\brsp80 Bravo\par");
        (pages.Top("Bravo") - pages.Top("Alpha")).Points.ShouldBe(Pitch + 4.5, 0.01);
        (pages.Top("Charlie") - pages.Top("Bravo")).Points.ShouldBe(Pitch, 0.01);
    }

    /// <summary>A side with no style word draws nothing and takes no room, whatever width it states.</summary>
    [Fact]
    public void ASideWithNoStyleWordIsNoBorder()
    {
        Layout pages = Lay(@"\pard\brdrb\brdrw30 Bravo\par");
        (pages.Top("Charlie") - pages.Top("Bravo")).Points.ShouldBe(Pitch, 0.01);
        pages.Paragraph("Bravo").Borders.ShouldBeNull();
    }

    /// <summary>A styled side with no <c>\brdrw</c> is 0.75 pt; a double at 15 twips is three bands of it.</summary>
    [Theory]
    [InlineData(@"\brdrs", 0.75)]
    [InlineData(@"\brdrdb\brdrw15", 2.25)]
    [InlineData(@"\brdrs\brdrw30\brsp40", 3.5)]
    public void ABottomRuleTakesWhatItsStyleDraws(string words, double room)
    {
        Layout pages = Lay(@"\pard\brdrb" + words + @" Bravo\par");
        (pages.Top("Charlie") - pages.Top("Bravo")).Points.ShouldBe(Pitch + room, 0.01);
    }

    /// <summary>
    /// A lineless side's spacing counts once another side draws, and not when none does.
    /// </summary>
    /// <remarks>
    /// <c>SvxBoxItem::CalcLineSpace(…, bEvenIfNoLine=true)</c>; measured: the same <c>\brsp100</c> bottom
    /// moves the next paragraph 5 pt beside a drawn top and not at all alone.
    /// </remarks>
    [Theory]
    [InlineData(@"\brdrt\brdrs\brdrw10\brdrb\brdrw30\brsp100", 5.0)]
    [InlineData(@"\brdrb\brdrw30\brsp100", 0.0)]
    public void ALinelessSidesSpacingCountsOnlyBesideADrawnSide(string words, double room)
    {
        Layout pages = Lay(@"\pard" + words + @" Bravo\par");
        (pages.Top("Charlie") - pages.Top("Bravo")).Points.ShouldBe(Pitch + room, 0.01);
    }

    /// <summary>
    /// A box narrows its text by each side's rule and spacing, and leaves its own edge at the indent.
    /// </summary>
    /// <remarks>
    /// The reference sets <c>\li720\ri720\box\brdrs\brdrw20\brsp60</c> text at 112.10 against the indent's
    /// 108.10 and strokes the left rule's centre at 108.50 — the inverse of the DOCX path, where the
    /// box grows outward.
    /// </remarks>
    [Fact]
    public void ABoxNarrowsItsTextAndSitsAtTheIndents()
    {
        Layout pages = Lay(@"\pard\li720\ri720\box\brdrs\brdrw20\brsp60 Bravo\par");
        PageParagraph bravo = pages.Paragraph("Bravo");

        bravo.DeclaredFormat.StartIndent.Points.ShouldBe(36 + 4, 0.01);
        bravo.DeclaredFormat.EndIndent.Points.ShouldBe(36 + 4, 0.01);
        bravo.Borders!.InsideIndents.ShouldBeTrue();

        DocRect left = pages.Fills().First(fill => fill.Width.Points < 2 && fill.Height.Points > 2);
        left.X.Points.ShouldBe(72 + 36, 0.01);
    }

    /// <summary>Two alike boxes are one, with neither the upper's bottom nor the lower's top.</summary>
    [Fact]
    public void TwoAlikeBoxesJoin()
    {
        Layout pages = Lay(@"\pard\box\brdrs\brdrw20\brsp60 Bravo\par\pard\box\brdrs\brdrw20\brsp60 Delta\par");
        (pages.Top("Delta") - pages.Top("Bravo")).Points.ShouldBe(Pitch, 0.01);
        pages.Paragraph("Delta").Borders!.JoinsAbove.ShouldBeTrue();
    }

    /// <summary>
    /// A between rule is the second paragraph's top, after the first's bottom spacing and before its own.
    /// </summary>
    /// <remarks>Reference: 3 pt of box spacing, a 0.5 pt between rule and 5 pt of its own make 8.5 pt.</remarks>
    [Fact]
    public void ABetweenRuleBecomesTheSecondParagraphsTop()
    {
        const string boxed = @"\box\brdrs\brdrw20\brsp60\brdrbtw\brdrs\brdrw10\brsp100";
        Layout pages = Lay(@"\pard" + boxed + @" Bravo\par\pard" + boxed + @" Delta\par");
        (pages.Top("Delta") - pages.Top("Bravo")).Points.ShouldBe(Pitch + 8.5, 0.01);
    }

    /// <summary>
    /// The join asks the upper paragraph's flag: a box over a between-box joins, the reverse does not.
    /// </summary>
    [Theory]
    [InlineData(false, true, 0.0)]
    [InlineData(true, false, 8.0)]
    public void TheJoinAsksTheUpperParagraphsConnectFlag(bool upperBetween, bool lowerBetween, double room)
    {
        const string box = @"\box\brdrs\brdrw20\brsp60";
        const string between = @"\brdrbtw\brdrs\brdrw10";
        Layout pages = Lay(
            @"\pard" + box + (upperBetween ? between : "") + @" Bravo\par"
            + @"\pard" + box + (lowerBetween ? between : "") + @" Delta\par");

        (pages.Top("Delta") - pages.Top("Bravo")).Points.ShouldBe(Pitch + room, 0.01);
    }

    /// <summary><c>\cbpat</c> shades the paragraph; with a box the fill reaches the box's outer edge.</summary>
    [Fact]
    public void AShadedBoxIsFilledToItsOuterEdge()
    {
        Layout pages = Lay(@"\pard\cbpat3\box\brdrs\brdrw20\brsp60 Bravo\par");
        pages.Paragraph("Bravo").Shading.ShouldBe(Colour.FromRgb(0xFFFF00));

        DocRect fill = pages.Fills().OrderByDescending(fill => fill.Width.Points * fill.Height.Points).First();
        fill.X.Points.ShouldBe(72, 0.01);
        fill.Width.Points.ShouldBe(468, 0.01);
        fill.Height.Points.ShouldBe(Pitch + 8, 0.06);
    }

    /// <summary><c>\cbpat0</c> is no shading, as writerfilter's <c>if (nParam)</c> says.</summary>
    [Fact]
    public void AZeroPatternIsNoShading()
        => Lay(@"\pard\cbpat0 Bravo\par").Paragraph("Bravo").Shading.ShouldBeNull();

    /// <summary><c>\pard</c> clears the borders, so the next paragraph is plain.</summary>
    [Fact]
    public void PardClearsTheBorders()
    {
        Layout pages = Lay(@"\pard\brdrb\brdrs\brdrw30 Bravo\par\pard\brdrw60 Delta\par");
        pages.Paragraph("Delta").Borders.ShouldBeNull();
    }

    /// <summary>A cell's selector takes the describing words, so a row's border stays the cell's.</summary>
    [Fact]
    public void ACellSelectorTakesTheDescribingWords()
    {
        Layout pages = Lay(
            @"\pard\brdrb\brdrs Bravo\par"
            + @"\trowd\clbrdrb\brdrs\brdrw40\cellx4000\pard\intbl Delta\cell\row\pard");

        pages.Paragraph("Bravo").Borders!.Bottom!.Value.Width.Points.ShouldBe(0.75, 0.01);
    }

    private static Layout Lay(string body)
    {
        string rtf =
            @"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}"
            + @"{\colortbl;\red255\green0\blue0;\red0\green0\blue255;\red255\green255\blue0;}"
            + @"\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
            + @"\pard\fs24 Alpha\par " + body + @"\pard Charlie\par}";

        using DocumentSource source = DocumentSource.FromStream(
            new MemoryStream(Encoding.ASCII.GetBytes(rtf)), "borders.rtf");
        using IDocument document = new WordProcessingReader().Read(source);
        return new Layout((WordProcessingPages)((IPaginatedDocument)document).Layout());
    }

    private sealed class Layout(WordProcessingPages pages)
    {
        public Length Top(string text) => pages.Pages[0].Lines
            .First(line => pages.TextOf(line).Contains(text, StringComparison.Ordinal)).Top;

        public PageParagraph Paragraph(string text)
            => pages.Paragraphs.First(paragraph => paragraph.Text.Contains(text, StringComparison.Ordinal));

        public List<DocRect> Fills()
        {
            PlacedDrawingSink sink = new();
            PageDrawing.Draw(pages.Pages[0], pages.Blocks, sink);
            return [.. sink.Fills.Select(fill => fill.Bounds)];
        }
    }
}
