using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// tdf#168777: a line's trailing blanks carry the underline of the text before them where the document
/// asks — in a DOCX, <c>w:ulTrailSpace</c> — and in a PDF that is one blank's worth of rule.
/// </summary>
/// <remarks>
/// Measured on 26.8.1.1 with an authored DOCX in two copies, with and without the flag: the underlined
/// line ending in four blanks gets a 2.40 pt rule after its text with the flag and none without, and a
/// wrapped line gets the same one blank where it fits the line. A highlight is not extended either way.
/// </remarks>
public sealed class TrailingBlankDecorationTests
{
    private static readonly Length Size = Length.FromPoints(11);

    /// <summary>With the flag, one blank of underline follows the text; without it, nothing.</summary>
    [Fact]
    public void OneBlankOfUnderlineFollowsTheTextWhereTheDocumentAsks()
    {
        List<(DocRect Area, Colour Colour)> on = Rules(decorates: true);
        List<(DocRect Area, Colour Colour)> off = Rules(decorates: false);

        off.Count.ShouldBe(1);
        on.Count.ShouldBe(2);
        on[1].Area.X.ShouldBe(on[0].Area.Right);
        on[1].Area.Width.ShouldBeGreaterThan(Length.FromPoints(2));
        on[1].Area.Width.ShouldBeLessThan(Length.FromPoints(3), "one blank, not four");
    }

    /// <summary>The DOCX reader sets the flag from <c>w:compat/w:ulTrailSpace</c>.</summary>
    [Theory]
    [InlineData("<w:compat><w:ulTrailSpace/></w:compat>", true)]
    [InlineData("<w:compat/>", false)]
    public void TheDocxReaderTakesItFromTheCompatibilitySettings(string settings, bool expected)
        => MiniDocx.Layout("<w:p><w:r><w:t>A</w:t></w:r></w:p>", settings: settings)
            .Blocks.OfType<PageParagraph>().First().DecoratesTrailingBlank.ShouldBe(expected);

    private static List<(DocRect Area, Colour Colour)> Rules(bool decorates)
    {
        const string Text = "Ends with blanks    ";
        PageParagraph paragraph = new()
        {
            Text = Text,
            Face = Face,
            EmSize = Size,
            Runs = [new PageRun(0, Text.Length, Face, Size, Colour: Colour.Black, Underline: TextUnderline.SingleLine)],
            DecoratesTrailingBlank = decorates,
        };

        PlacedLine line = new(
            ParagraphIndex: 0,
            LineIndex: 0,
            Box: new LineBox(
                new TextLine(0, Text.Length, Text.TrimEnd().Length, Length.Zero, EndsParagraph: true),
                Length.Zero,
                Length.Zero,
                Length.FromPoints(14),
                Length.FromPoints(11),
                Length.Zero),
            Top: Length.Zero);

        List<(DocRect Area, Colour Colour)> rules = [];
        PageDrawing.RunsIn(
            new DocRect(Length.Zero, Length.Zero, Length.FromPoints(400), Length.FromPoints(400)),
            line, paragraph, rules: rules);
        return rules;
    }

    private static OpenTypeFace Face { get; } = Resolve();

    private static OpenTypeFace Resolve()
    {
        SystemFontResolver resolver = new(SystemFontIndex.Build());
        return resolver.LoadOpenType(resolver.Resolve(new FontRequest("Liberation Serif", 400, false)));
    }
}
