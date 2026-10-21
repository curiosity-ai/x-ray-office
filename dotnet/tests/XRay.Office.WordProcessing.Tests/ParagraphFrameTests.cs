using System.IO.Compression;
using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Units;
using XRay.Office.TestKit;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// DOCX <c>w:framePr</c> paragraph frames, placed where the 26.8 daily places them.
/// </summary>
/// <remarks>
/// <para>
/// Every expectation is a measurement of LibreOfficeDev 26.8.3.0.0 (9dcd8a837588) rendering the same
/// document to PDF under stock fontconfig: A4, one-inch margins, Liberation Serif 12 pt, a paragraph of
/// text, the framed paragraph, then two paragraphs of text. The frame in most cases is
/// <c>w:w="2880" w:wrap="around" w:vAnchor="text" w:hAnchor="text" w:x="1440" w:y="0"</c>.
/// Positions are PDF points from the page's top-left corner, baselines read from the reference's text
/// objects; the reference's text starts 0.1 pt to the right of ours throughout (its PDF writer rounds the
/// origin), which the tolerance covers.
/// </para>
/// <para>
/// The rules themselves are writerfilter's: <c>MakeFrameProperties</c> resolves each attribute over the
/// paragraph and its style chain (<c>DomainMapper_Impl.cxx</c>:1873-2083), consecutive paragraphs whose
/// resolved attributes are equal share one frame (:2477, :2553), and only a paragraph that states its
/// own <c>w:framePr</c> is framed at all (<c>DomainMapper.cxx</c>:2831-2843).
/// </para>
/// </remarks>
public sealed class ParagraphFrameTests
{
    private const double Tolerance = 0.2;

    private const string Frame =
        "w:w=\"2880\" w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"text\" w:x=\"1440\" w:y=\"0\"";

    private const string Framed = "FRAMEA one two three four five six seven eight nine ten";

    private const string Lorem =
        "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore "
        + "et dolore magna aliqua Ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut "
        + "aliquip ex ea commodo consequat Duis aute irure dolor in reprehenderit in voluptate velit esse "
        + "cillum dolore eu fugiat nulla pariatur Excepteur sint occaecat cupidatat non proident sunt in "
        + "culpa qui officia deserunt";

    /// <summary>
    /// The frame stands one inch into the column on the line the paragraph after it starts on, and that
    /// paragraph's text runs around its right side.
    /// </summary>
    [Fact]
    public void AFrameStandsAtItsOffsetAndTheTextAfterItWrapsAroundIt()
    {
        List<DrawnWord> words = Words(Body(Frame));

        DrawnWord frame = words.Single(word => word.Text == "FRAMEA");
        frame.Left.ShouldBe(144.1, Tolerance);
        frame.Baseline.ShouldBe(110.80, Tolerance);

        // The reference's line at 110.8 pt has text left of the frame and resumes at 288.1 pt, right of it.
        words.Where(word => Math.Abs(word.Baseline - 110.8) < 0.5 && word.Text != "FRAMEA")
            .Max(word => word.Left).ShouldBeGreaterThan(280);
        words.Where(word => Math.Abs(word.Baseline - 110.8) < 0.5)
            .Min(word => word.Left).ShouldBe(72.1, Tolerance);
    }

    /// <summary><c>w:xAlign="right"</c> on a column-relative frame puts it against the right margin.</summary>
    [Fact]
    public void ARightAlignedFrameEndsAtTheColumnsRightEdge()
    {
        List<DrawnWord> words = Words(Body(Frame.Replace("w:x=\"1440\"", "w:xAlign=\"right\"", StringComparison.Ordinal)));

        words.Single(word => word.Text == "FRAMEA").Left.ShouldBe(379.4, Tolerance);
    }

    /// <summary>
    /// <c>w:wrap="notBeside"</c> — and <c>tight</c>, which writerfilter maps the same way — leaves no text
    /// beside the frame.
    /// </summary>
    [Theory]
    [InlineData("notBeside")]
    [InlineData("tight")]
    public void ANotBesideFrameKeepsTheTextAboveAndBelowIt(string wrap)
    {
        List<DrawnWord> words = Words(Body(Frame.Replace("around", wrap, StringComparison.Ordinal)));

        // The frame's two lines sit at 110.8 and 124.6 pt, and the paragraph after it resumes at 138.4.
        words.Where(word => word.Baseline is > 103 and < 132 && word.Left < 140).ShouldBeEmpty();
        words.First(word => word.Text == "AFTER").Baseline.ShouldBe(138.4, Tolerance);
    }

    /// <summary>
    /// A <c>w:framePr</c> stated only in the paragraph's style frames nothing: the paragraph stays in
    /// the flow, where the reference lays it out.
    /// </summary>
    [Fact]
    public void AFrameStatedOnlyInTheStyleLeavesTheParagraphInTheFlow()
    {
        string style = "<w:style w:type=\"paragraph\" w:styleId=\"Framed\"><w:name w:val=\"Framed\"/>"
                       + $"<w:basedOn w:val=\"Normal\"/><w:pPr><w:framePr {Frame}/></w:pPr></w:style>";
        string body = P("BEFORE paragraph text.") + P(Framed, "<w:pStyle w:val=\"Framed\"/>") + P("AFTER " + Lorem);

        DrawnWord frame = Words(body, style).Single(word => word.Text == "FRAMEA");

        frame.Left.ShouldBe(72.1, Tolerance);
        frame.Baseline.ShouldBe(97.00, Tolerance);
    }

    /// <summary><c>w:yAlign="inline"</c> turns frame mode back off (bnc#780851).</summary>
    [Fact]
    public void AnInlineFrameIsAnOrdinaryParagraph()
    {
        List<DrawnWord> words = Words(Body(Frame.Replace("w:y=\"0\"", "w:yAlign=\"inline\"", StringComparison.Ordinal)));

        DrawnWord frame = words.Single(word => word.Text == "FRAMEA");
        frame.Left.ShouldBe(72.1, Tolerance);
        frame.Baseline.ShouldBe(110.80, Tolerance);
    }

    /// <summary>Two consecutive paragraphs with the same frame properties share one frame.</summary>
    [Fact]
    public void ConsecutiveEqualFramesAreOneFrame()
    {
        string body = P("BEFORE paragraph text. " + Lorem[..150])
                      + P(Framed, $"<w:framePr {Frame}/>")
                      + P("FRAMEB second framed paragraph", $"<w:framePr {Frame}/>")
                      + P("AFTER " + Lorem);

        List<DrawnWord> words = Words(body);

        DrawnWord second = words.Single(word => word.Text == "FRAMEB");
        second.Left.ShouldBe(144.1, Tolerance);
        second.Baseline.ShouldBe(138.40, Tolerance);
    }

    /// <summary>
    /// A bordered frame keeps the paragraph's border, so its text moves in by the border and its
    /// spacing, and DOCX widens the frame by the side spacing (<c>lcl_MoveBorderPropertiesToFrame</c>,
    /// <c>DomainMapper_Impl.cxx</c>:1768-1843): the text beside it resumes at 296.1 pt, not 288.1.
    /// </summary>
    [Fact]
    public void ABorderedFrameInsetsItsTextAndGrowsByItsSpacing()
    {
        const string side = "w:val=\"single\" w:sz=\"8\" w:space=\"4\" w:color=\"000000\"";
        string borders = $"<w:pBdr><w:top {side}/><w:left {side}/><w:bottom {side}/><w:right {side}/></w:pBdr>";

        List<DrawnWord> words = Words(Body(Frame, borders));

        DrawnWord frame = words.Single(word => word.Text == "FRAMEA");
        frame.Left.ShouldBe(149.1, Tolerance);
        frame.Baseline.ShouldBe(115.80, Tolerance);
        words.Where(word => Math.Abs(word.Baseline - 110.8) < 0.5 && word.Left > 200)
            .Min(word => word.Left).ShouldBe(296.1, Tolerance);
    }

    /// <summary>
    /// The attribute rules in isolation: an absent <c>w:vAnchor</c> is the margin, unless the frame
    /// states no <c>w:y</c> and no <c>w:yAlign</c>, which makes it the text (<c>DomainMapper_Impl.cxx</c>:
    /// 2009-2018); an absent <c>w:hAnchor</c> is the column.
    /// </summary>
    [Fact]
    public void AnAbsentAnchorResolvesAsWriterfilterResolvesIt()
    {
        ParagraphFramePosition noY = ParagraphFrameLayer.Resolve([new ParagraphFrameLayer { Width = 2880 }]);
        ParagraphFramePosition withY = ParagraphFrameLayer.Resolve([new ParagraphFrameLayer { Width = 2880, Y = 2880 }]);

        noY.VerticalAnchor.ShouldBe(ParagraphFrameRelation.Text);
        noY.HorizontalAnchor.ShouldBe(ParagraphFrameRelation.Text);
        withY.VerticalAnchor.ShouldBe(ParagraphFrameRelation.Margin);
        withY.Y.ShouldBe(Length.FromTwips(2880));
    }

    /// <summary>
    /// A three-line drop cap: the letter is scaled until its ink spans the first two lines and the third
    /// line's ascent, its baseline is the third line's, and the first three lines start after it.
    /// </summary>
    [Fact]
    public void AThreeLineDropCapSpansThreeLines()
    {
        List<DrawnWord> words = Words(DropCap("w:lines=\"3\""));

        DrawnWord letter = words.Single(word => word.Size > 30);
        letter.Text.ShouldBe("W");
        letter.Size.ShouldBe(57.9, 0.15);
        letter.Baseline.ShouldBe(124.6, Tolerance);
        words.Single(word => word.Text == "AFTER").Left.ShouldBe(126.7, Tolerance);
    }

    /// <summary>An absent <c>w:lines</c> is two lines (<c>DomainMapper_Impl.cxx</c>:2528-2534).</summary>
    [Fact]
    public void ADropCapWithNoLineCountSpansTwo()
    {
        DrawnWord letter = Words(DropCap(string.Empty)).Single(word => word.Size > 20);

        letter.Size.ShouldBe(37.3, 0.15);
        letter.Baseline.ShouldBe(110.8, Tolerance);
    }

    /// <summary>
    /// A drop of one line is no drop: the letter joins the paragraph's first line at the text's size.
    /// </summary>
    [Fact]
    public void AOneLineDropCapIsSetInTheText()
    {
        List<DrawnWord> words = Words(DropCap("w:lines=\"1\""));

        words.ShouldAllBe(word => word.Size < 13);
        words.Single(word => word.Text == "WAFTER").Baseline.ShouldBe(97.0, Tolerance);
    }

    /// <summary>
    /// A quotation mark ahead of the letter is measured from the baseline under the drop-cap punctuation
    /// rule DOCX import turns on (<c>WriterFilter.cxx</c>:334, <c>txtdrop.cxx</c>:864-868), so the pair
    /// drops at a letter's size.
    /// </summary>
    [Fact]
    public void AQuotationMarkDropsAtALettersSize()
    {
        DrawnWord letters = Words(DropCap("w:lines=\"3\"", "“W")).Single(word => word.Size > 30);

        letters.Size.ShouldBe(57.0, 0.15);
        letters.Baseline.ShouldBe(124.6, Tolerance);
    }

    /// <summary>The body of the frame probes: a paragraph, the framed one, and two after it.</summary>
    private static string Body(string frame, string extraProperties = "")
        => P("BEFORE paragraph text. " + Lorem[..150])
           + P(Framed, $"<w:framePr {frame}/>" + extraProperties)
           + P("AFTER " + Lorem)
           + P("SECOND " + Lorem);

    /// <summary>A drop-cap paragraph holding the letters, then the paragraph they drop into.</summary>
    private static string DropCap(string lines, string letters = "W")
        => P("BEFORE text")
           + P(letters, $"<w:framePr w:dropCap=\"drop\" {lines} w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"text\"/>")
           + P("AFTER " + Lorem);

    private static string P(string text, string properties = "")
        => $"<w:p><w:pPr>{properties}</w:pPr><w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r></w:p>";

    /// <summary>The words on the first page of the document the body makes.</summary>
    internal static List<DrawnWord> Words(string body, string styles = "")
        => Draw(Docx(body, styles), "probe.docx");

    /// <summary>The words on the first page of a document.</summary>
    internal static List<DrawnWord> Draw(byte[] bytes, string name)
    {
        RecordingDrawingSink sink = new();

        using (DocumentSource source = DocumentSource.FromStream(new MemoryStream(bytes), name))
        {
            using IDocument read = new WordProcessingReader().Read(source);
            IPageSequence pages = ((IPaginatedDocument)read).Layout();
            pages[0].Draw(sink);
        }

        return DrawnWords.On(sink.Pages[0]);
    }

    private static byte[] Docx(string body, string styles)
    {
        const string ns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        const string head = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

        using MemoryStream result = new();
        using (ZipArchive archive = new(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml",
                head + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
                + "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/></Types>");
            Write(archive, "_rels/.rels",
                head + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Write(archive, "word/_rels/document.xml.rels",
                head + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rS\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Write(archive, "word/styles.xml",
                head + $"<w:styles {ns}><w:docDefaults><w:rPrDefault><w:rPr>"
                + "<w:rFonts w:ascii=\"Liberation Serif\" w:hAnsi=\"Liberation Serif\" w:cs=\"Liberation Serif\"/>"
                + "<w:sz w:val=\"24\"/><w:lang w:val=\"en-US\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr/></w:pPrDefault></w:docDefaults>"
                + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>"
                + styles + "</w:styles>");
            Write(archive, "word/document.xml",
                head + $"<w:document {ns}><w:body>{body}"
                + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" "
                + "w:bottom=\"1440\" w:left=\"1440\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr>"
                + "</w:body></w:document>");
        }

        return result.ToArray();

        static void Write(ZipArchive archive, string name, string content)
        {
            using Stream entry = archive.CreateEntry(name).Open();
            entry.Write(Encoding.UTF8.GetBytes(content));
        }
    }
}
