using System.Xml.Linq;
using XRay.Office.Core.Extraction;
using XRay.Office.Ooxml.DrawingML;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// Tests the shared DrawingML text-body reader from markup literals.
/// </summary>
/// <remarks>
/// The reader lives in <c>XRay.Office.Ooxml</c> because a text body is identical in a deck, a
/// spreadsheet drawing and a Word shape. These tests exercise it as such — nothing here is
/// PresentationML — so they stay valid when the spreadsheet and word-processing paths start
/// calling it.
/// </remarks>
public class DrawingTextBodyTests
{
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private static XElement Parse(string inner)
        => XElement.Parse($"<a:txBody xmlns:a=\"{A}\" xmlns:r=\"http://schemas.openxmlformats.org/"
                          + $"officeDocument/2006/relationships\">{inner}</a:txBody>");

    private static XElement Level(string inner)
        => XElement.Parse($"<a:lvl1pPr xmlns:a=\"{A}\">{inner}</a:lvl1pPr>");

    private static List<ContentParagraph> Read(XElement body, DrawingTextOptions? options = null)
    {
        ContentSection target = new() { Kind = SectionKind.Frame };
        DrawingTextBody.Read(body, target, options);
        return [.. target.Children.OfType<ContentParagraph>()];
    }

    [Fact]
    public void APlainTextBoxIsNotBulleted()
    {
        List<ContentParagraph> paragraphs = Read(Parse("<a:p><a:r><a:t>Hello</a:t></a:r></a:p>"));

        // The schema's default is no bullet. A reader that fell through to the master's body
        // style for every shape would bullet every text box on every slide.
        paragraphs.Single().ListMarker.ShouldBeNull();
        paragraphs.Single().ListLevel.ShouldBeNull();
    }

    [Fact]
    public void ABulletIsInheritedForTheParagraphsOwnLevelAndNotAnother()
    {
        XElement body = Parse(
            "<a:p><a:pPr lvl=\"1\"/><a:r><a:t>Nested</a:t></a:r></a:p>");

        // The inherited chain is asked for level 1 and answers with that level's entry. The
        // off-by-one that makes lvl2pPr look like level 2 is what this pins down: the caller is
        // handed the zero-based level and returns the element it decided belongs to it.
        List<int> asked = [];
        List<ContentParagraph> paragraphs = Read(body, new DrawingTextOptions
        {
            InheritedLevelProperties = level =>
            {
                asked.Add(level);
                return level == 1 ? [Level("<a:buChar char=\"▪\"/>")] : [];
            },
        });

        asked.ShouldBe([1]);
        paragraphs.Single().ListMarker.ShouldBe("▪");
        paragraphs.Single().ListLevel.ShouldBe(1);
    }

    [Fact]
    public void TheBodysOwnListStyleBeatsWhatIsInherited()
    {
        XElement body = Parse(
            "<a:lstStyle><a:lvl1pPr><a:buChar char=\"-\"/></a:lvl1pPr></a:lstStyle>"
            + "<a:p><a:r><a:t>Item</a:t></a:r></a:p>");

        Read(body, new DrawingTextOptions
        {
            InheritedLevelProperties = _ => [Level("<a:buChar char=\"•\"/>")],
        }).Single().ListMarker.ShouldBe("-");
    }

    [Fact]
    public void ABulletNoneStopsTheSearchRatherThanBeingSkipped()
    {
        XElement body = Parse("<a:p><a:pPr><a:buNone/></a:pPr><a:r><a:t>Title</a:t></a:r></a:p>");

        // A master's title placeholder is exactly a buNone sitting over a body style that
        // bullets. Treating buNone as "says nothing" and continuing down the chain puts a bullet
        // on every title in the deck.
        Read(body, new DrawingTextOptions
        {
            InheritedLevelProperties = _ => [Level("<a:buChar char=\"•\"/>")],
        }).Single().ListMarker.ShouldBeNull();
    }

    [Fact]
    public void APrivateUseAreaBulletBecomesARealBullet()
    {
        // U+F0B7 is Symbol's bullet and U+F06C is Wingdings', both meaningless outside their
        // font. LibreOffice's own HTML export passes them straight through, which produces text
        // no consumer can interpret; this agrees with the ODP path instead.
        Read(Parse("<a:p><a:pPr><a:buChar char=\"\"/></a:pPr><a:r><a:t>x</a:t></a:r></a:p>"))
            .Single().ListMarker.ShouldBe("•");
    }

    [Theory]
    [InlineData("arabicPeriod", "1.")]
    [InlineData("arabicParenR", "1)")]
    [InlineData("arabicParenBoth", "(1)")]
    [InlineData("arabicPlain", "1")]
    [InlineData("alphaLcPeriod", "a.")]
    [InlineData("alphaUcParenR", "A)")]
    [InlineData("romanLcPeriod", "i.")]
    [InlineData("romanUcParenBoth", "(I)")]
    // Not in LibreOffice's own switch, so its punctuation still has to come out right from the
    // scheme name rather than from a case that was never written.
    [InlineData("hindiNumPeriod", "1.")]
    public void EveryAutonumberSchemeIsDecomposedIntoAnAlphabetAndAPunctuation(
        string scheme, string expected)
    {
        Read(Parse($"<a:p><a:pPr><a:buAutoNum type=\"{scheme}\"/></a:pPr>"
                   + "<a:r><a:t>x</a:t></a:r></a:p>"))
            .Single().ListMarker.ShouldBe(expected);
    }

    [Fact]
    public void NumberingCountsPerLevelAndRestartsWhenItsParentAdvances()
    {
        XElement body = Parse(
            Item(0) + Item(1) + Item(1) + Item(0) + Item(1));

        Read(body).Select(p => p.ListMarker).ShouldBe(["1.", "a.", "b.", "2.", "a."]);

        static string Item(int level)
            => $"<a:p><a:pPr lvl=\"{level}\"><a:buAutoNum type=\""
               + (level == 0 ? "arabicPeriod" : "alphaLcPeriod")
               + "\"/></a:pPr><a:r><a:t>x</a:t></a:r></a:p>";
    }

    [Fact]
    public void StartAtAppliesToTheFirstItemOfARunAndNotToEveryItem()
    {
        XElement body = Parse(
            "<a:p><a:pPr><a:buAutoNum type=\"arabicPeriod\" startAt=\"5\"/></a:pPr>"
            + "<a:r><a:t>x</a:t></a:r></a:p>"
            + "<a:p><a:pPr><a:buAutoNum type=\"arabicPeriod\" startAt=\"5\"/></a:pPr>"
            + "<a:r><a:t>y</a:t></a:r></a:p>");

        Read(body).Select(p => p.ListMarker).ShouldBe(["5.", "6."]);
    }

    /// <summary>
    /// A list continues while its numbering type and <c>startAt</c> repeat, and restarts at its
    /// own <c>startAt</c> when either changes — <c>n90255.pptx</c>'s shape.
    /// </summary>
    /// <remarks>
    /// <c>ListNumberingState::markParagraph</c> (tdf#173712, <c>8f242675c1b1</c>, in 26.8.2). Five
    /// two-item lists at one level, each repeating its own <c>startAt</c>: 26.8.2 and PowerPoint
    /// draw <c>3 4 II III C D iv v f g</c>. Counting straight through, as this did, drew
    /// <c>3 4 V VI G H ix x k l</c>; 26.8.1.1 restarts every item that states <c>startAt</c> and
    /// draws <c>3 3 II II C C iv iv f f</c>, which is the reference's bug and is not ported.
    /// </remarks>
    [Fact]
    public void AListRestartsWhenItsTypeOrStartChangesAndContinuesWhileTheyRepeat()
    {
        XElement body = Parse(
            Item("arabicPeriod", 3) + Item("arabicPeriod", 3)
            + Item("romanUcPeriod", 2) + Item("romanUcPeriod", 2)
            + Item("alphaUcPeriod", 3) + Item("alphaUcPeriod", 3)
            + Item("romanLcPeriod", 4) + Item("romanLcPeriod", 4)
            + Item("alphaLcParenR", 6) + Item("alphaLcParenR", 6));

        Read(body).Select(p => p.ListMarker).ShouldBe(
            ["3.", "4.", "II.", "III.", "C.", "D.", "iv.", "v.", "f)", "g)"]);

        static string Item(string type, int start)
            => $"<a:p><a:pPr><a:buAutoNum type=\"{type}\" startAt=\"{start}\"/></a:pPr>"
               + "<a:r><a:t>x</a:t></a:r></a:p>";
    }

    /// <summary>
    /// A changed <c>startAt</c> on the same type restarts the list too, downwards as well as up.
    /// </summary>
    [Fact]
    public void AChangedStartAtRestartsTheList()
    {
        XElement body = Parse(
            Item(5) + Item(5) + Item(3) + Item(3) + Item(null));

        // An absent startAt counts as 1, so the last item is a change as well.
        Read(body).Select(p => p.ListMarker).ShouldBe(["5.", "6.", "3.", "4.", "1."]);

        static string Item(int? start)
            => "<a:p><a:pPr><a:buAutoNum type=\"arabicPeriod\""
               + (start is { } at ? $" startAt=\"{at}\"" : "")
               + "/></a:pPr><a:r><a:t>x</a:t></a:r></a:p>";
    }

    /// <summary>
    /// A bulleted paragraph ends the numbered lists at its own level and every deeper one.
    /// </summary>
    /// <remarks>
    /// <c>markParagraph</c> clears the levels from its own down for a paragraph that is not
    /// numbered, and the outliner's walk stops at a shallower paragraph whatever it holds
    /// (<c>Outliner::ImplGetNumbering</c>, <c>outliner.cxx:1676-1678</c>). Only a numbered parent
    /// used to restart its children.
    /// </remarks>
    [Fact]
    public void ABulletedParentRestartsTheNumberedListBelowIt()
    {
        XElement body = Parse(
            Numbered(1) + Numbered(1) + Bulleted(0) + Numbered(1));

        Read(body).Select(p => p.ListMarker).ShouldBe(["1.", "2.", "\u2022", "1."]);

        static string Numbered(int level)
            => $"<a:p><a:pPr lvl=\"{level}\"><a:buAutoNum type=\"arabicPeriod\"/></a:pPr>"
               + "<a:r><a:t>x</a:t></a:r></a:p>";

        static string Bulleted(int level)
            => $"<a:p><a:pPr lvl=\"{level}\"><a:buChar char=\"\u2022\"/></a:pPr>"
               + "<a:r><a:t>x</a:t></a:r></a:p>";
    }

    /// <summary>
    /// A paragraph that takes its numbering from outside the body continues the outliner's run
    /// but is not a list item to the importer, so a following item stating its own numbering
    /// restarts.
    /// </summary>
    /// <remarks>
    /// <c>markParagraph</c> looks at the paragraph's own bullet and then at its body's
    /// <c>a:lstStyle</c>, never at the layout's or the master's, and treats a paragraph with
    /// neither as unnumbered.
    /// </remarks>
    [Fact]
    public void NumberingFromOutsideTheBodyDoesNotContinueIntoAStatedList()
    {
        XElement inherited = Level("<a:buAutoNum type=\"arabicPeriod\"/>");
        DrawingTextOptions options = new() { InheritedLevelProperties = _ => [inherited] };

        XElement body = Parse(
            Plain() + Plain() + "<a:p><a:pPr><a:buAutoNum type=\"arabicPeriod\"/></a:pPr>"
                              + "<a:r><a:t>x</a:t></a:r></a:p>");

        Read(body, options).Select(p => p.ListMarker).ShouldBe(["1.", "2.", "1."]);

        static string Plain() => "<a:p><a:r><a:t>x</a:t></a:r></a:p>";
    }

    [Fact]
    public void AnEmptyParagraphDrawsNoMarkerAndConsumesNoNumber()
    {
        XElement body = Parse(
            Numbered("<a:r><a:t>one</a:t></a:r>")
            + Numbered("<a:endParaRPr lang=\"en-GB\"/>")
            + Numbered("<a:r><a:t>two</a:t></a:r>"));

        // The blank line an author leaves between two items is still an a:p and still inherits
        // the level's bullet, but nothing draws one on it. Measured against LibreOffice's layout
        // expectations for NumberedList-12ab-ab-34.pptx, whose trailing empty item produced a
        // stray "a." before this rule existed.
        Read(body).Select(p => p.ListMarker).ShouldBe(["1.", null, "2."]);

        static string Numbered(string inner)
            => "<a:p><a:pPr><a:buAutoNum type=\"arabicPeriod\"/></a:pPr>" + inner + "</a:p>";
    }

    [Fact]
    public void ALineBreakStaysInsideItsParagraph()
    {
        List<ContentParagraph> paragraphs = Read(Parse(
            "<a:p><a:r><a:t>one</a:t></a:r><a:br/><a:r><a:t>two</a:t></a:r></a:p>"));

        // a:br is a soft return: one paragraph, two lines. Splitting it into two paragraphs
        // would report a structure the file does not have.
        paragraphs.Count.ShouldBe(1);
        paragraphs[0].GetText().ShouldBe("one\ntwo\n");
    }

    [Fact]
    public void AFieldContributesItsCachedValueRatherThanBeingRecomputed()
    {
        List<ContentParagraph> paragraphs = Read(Parse(
            "<a:p><a:fld id=\"{x}\" type=\"slidenum\"><a:t>7</a:t></a:fld></a:p>"));

        // The cached a:t is what a reader saw. LibreOffice's HTML export writes its own
        // unresolved "<number>" placeholder here instead, which is the one place the reference
        // is further from the file than X-Ray.Office is.
        paragraphs.Single().GetText().ShouldBe("7\n");
    }

    [Fact]
    public void EmphasisIsReadFromValuesRatherThanFromAttributePresence()
    {
        List<ContentRun> runs = [.. Read(Parse(
            "<a:p><a:r><a:rPr b=\"1\" i=\"0\" u=\"none\" strike=\"noStrike\" baseline=\"30000\" "
            + "lang=\"en-GB\"/><a:t>x</a:t></a:r></a:p>"))
            .Single().Children.OfType<ContentRun>()];

        // LibreOffice's exporter writes strike="noStrike" on every run it emits, so a presence
        // test marks a whole deck struck through.
        RunEmphasis emphasis = runs.Single().Emphasis;
        emphasis.ShouldBe(RunEmphasis.Bold | RunEmphasis.Superscript);
        runs.Single().Language.ShouldBe("en-GB");
    }

    [Fact]
    public void AHyperlinkResolvesThroughTheCallersRelationships()
    {
        XElement body = XElement.Parse(
            $"<a:txBody xmlns:a=\"{A}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/"
            + "2006/relationships\"><a:p><a:r><a:rPr><a:hlinkClick r:id=\"rId9\"/></a:rPr>"
            + "<a:t>link</a:t></a:r></a:p></a:txBody>");

        // The relationship is declared by the part that holds the shape, which this reader never
        // sees — hence the delegate rather than a lookup here.
        Read(body, new DrawingTextOptions { ResolveHyperlink = id => id == "rId9" ? "https://x/" : null })
            .Single().Children.OfType<ContentRun>().Single()
            .HyperlinkTarget.ShouldBe("https://x/");
    }

    [Fact]
    public void AnActionWithNoRelationshipIsReportedAsTheActionItself()
    {
        Read(Parse("<a:p><a:r><a:rPr><a:hlinkClick action=\"ppaction://hlinkshowjump?jump=nextslide\"/>"
                   + "</a:rPr><a:t>next</a:t></a:r></a:p>"))
            .Single().Children.OfType<ContentRun>().Single()
            .HyperlinkTarget.ShouldBe("ppaction://hlinkshowjump?jump=nextslide");
    }

    [Fact]
    public void ASingleEmptyRunCountsAsAnEmptyBody()
    {
        // The distinction decides whether a slide placeholder is "left alone" — LibreOffice draws
        // the same line in TextBody::isEmpty, and a naive "has any a:r" test gets it wrong.
        DrawingTextBody.IsEmpty(Parse("<a:p><a:r><a:t></a:t></a:r></a:p>")).ShouldBeTrue();
        DrawingTextBody.IsEmpty(Parse("<a:p><a:endParaRPr/></a:p>")).ShouldBeTrue();
        DrawingTextBody.IsEmpty(Parse("<a:p><a:r><a:t>x</a:t></a:r></a:p>")).ShouldBeFalse();
        DrawingTextBody.IsEmpty(null).ShouldBeTrue();
    }

    [Fact]
    public void AMergedCellIsReportedOnceRatherThanOnceForEachCellItCovers()
    {
        XElement table = XElement.Parse(
            $"<a:tbl xmlns:a=\"{A}\">"
            + "<a:tblPr firstRow=\"1\"/>"
            + "<a:tblGrid><a:gridCol w=\"1\"/><a:gridCol w=\"1\"/><a:gridCol w=\"1\"/></a:tblGrid>"
            + "<a:tr><a:tc gridSpan=\"2\"><a:txBody><a:p><a:r><a:t>wide</a:t></a:r></a:p></a:txBody>"
            + "</a:tc><a:tc hMerge=\"1\"/><a:tc><a:txBody><a:p><a:r><a:t>c</a:t></a:r></a:p>"
            + "</a:txBody></a:tc></a:tr></a:tbl>");

        ContentTable read = DrawingTable.Read(table);

        // The grid stays rectangular in the file — the covered cell is written out — so a reader
        // that emits it puts an empty cell after every merged one.
        read.ColumnCount.ShouldBe(3);
        read.HeaderRowCount.ShouldBe(1);

        ContentTableRow row = read.Children.OfType<ContentTableRow>().Single();
        row.Children.Count.ShouldBe(2);
        row.Children.OfType<ContentTableCell>().Select(c => c.Column).ShouldBe([0, 2]);
        row.Children.OfType<ContentTableCell>().First().ColumnSpan.ShouldBe(2);
        row.GetText().ShouldBe("wide\tc\n");
    }
}
