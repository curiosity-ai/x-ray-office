using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using XRay.Office.TestKit;
using XRay.Office.WordProcessing.OpenDocument;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// ODF <c>style:drop-cap</c>, measured against the 26.8 daily.
/// </summary>
/// <remarks>
/// <para>
/// Every expectation is a measurement of LibreOfficeDev 26.8.3.0.0 (9dcd8a837588) rendering the same
/// document under stock fontconfig: A4, 2.54 cm margins, Liberation Serif 12 pt, a paragraph, the
/// paragraph carrying the drop, and a paragraph after it. The size is <c>SwDropCapCache::CalcFontSize</c>'s
/// (<c>sw/source/core/text/txtdrop.cxx</c>:714-992): the letters are scaled until their ink, measured on
/// the glyphs' own bounding boxes, spans the first lines and the last line's ascent.
/// </para>
/// </remarks>
public sealed class OdtDropCapTests
{
    private const double Tolerance = 0.2;

    private const string Lorem =
        "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore "
        + "et dolore magna aliqua Ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut "
        + "aliquip ex ea commodo consequat Duis aute irure dolor in reprehenderit in voluptate velit esse "
        + "cillum dolore eu fugiat nulla pariatur Excepteur sint occaecat cupidatat non proident sunt in "
        + "culpa qui officia deserunt";

    /// <summary>A one-character, three-line drop: the same letter at the same size as DOCX's.</summary>
    [Fact]
    public void AOneCharacterDropSpansThreeLines()
    {
        List<DrawnWord> words = Draw(Fodt("style:lines=\"3\" style:length=\"1\""));

        DrawnWord letter = words.Single(word => word.Size > 30);
        letter.Text.ShouldBe("W");
        letter.Size.ShouldBe(57.9, 0.15);
        letter.Baseline.ShouldBe(124.6, Tolerance);
        words.Single(word => word.Text == "AFTER").Left.ShouldBe(126.7, Tolerance);
    }

    /// <summary>
    /// A whole-word drop is not measured from the baseline — the punctuation rule is for a counted drop
    /// only (<c>txtdrop.cxx</c>:800-802) — so its ink's descent counts and the word is raised by it
    /// (<c>pDrop-&gt;SetY</c>, :991): 123.7 pt where a single letter stands at 124.6.
    /// </summary>
    [Fact]
    public void AWordDropIsRaisedByItsInksDescent()
    {
        DrawnWord word = Draw(Fodt("style:lines=\"3\" style:length=\"word\"")).Single(drawn => drawn.Size > 30);

        word.Text.ShouldBe("WAFTER");
        word.Size.ShouldBe(57.4, 0.15);
        word.Baseline.ShouldBe(123.7, Tolerance);
    }

    /// <summary>
    /// A lone quotation mark drops at a letter's size under the punctuation rule a document with no
    /// settings keeps.
    /// </summary>
    [Fact]
    public void AQuotationMarkDropsAtALettersSize()
    {
        DrawnWord mark = Draw(Fodt("style:lines=\"3\" style:length=\"1\"", "“WAFTER "))
            .Single(word => word.Size > 30);

        mark.Size.ShouldBe(57.0, 0.15);
        mark.Baseline.ShouldBe(124.6, Tolerance);
    }

    /// <summary>
    /// A document whose configuration settings lack <c>DropCapPunctuation</c> predates the rule
    /// and is laid out the old way: the quotation mark's ink alone fills the drop's height, and its ink's
    /// foot stands on the third line's baseline.
    /// </summary>
    [Fact]
    public void AnOldDocumentBlowsAQuotationMarkUpToFillTheDrop()
    {
        DrawnWord mark = Draw(Odt(dropCapPunctuation: null), "probe.odt").Single(word => word.Size > 30);

        mark.Size.ShouldBe(150.9, 0.6);
        mark.Baseline.ShouldBe(186.35, 0.3);
    }

    /// <summary>The same packaged document stating the rule draws the mark at a letter's size.</summary>
    [Fact]
    public void ANewDocumentKeepsTheQuotationMarkOnTheBaseline()
    {
        DrawnWord mark = Draw(Odt(dropCapPunctuation: true), "probe.odt").Single(word => word.Size > 30);

        mark.Size.ShouldBe(57.0, 0.15);
    }

    /// <summary>The settings rule in isolation.</summary>
    [Fact]
    public void ThePunctuationRuleIsOffOnlyWhenConfigurationSettingsOmitIt()
    {
        OdtLayoutSource.DropCapPunctuation(null).ShouldBeTrue();
        OdtLayoutSource.DropCapPunctuation(XElement.Parse(Settings(null))).ShouldBeFalse();
        OdtLayoutSource.DropCapPunctuation(XElement.Parse(Settings(true))).ShouldBeTrue();
        OdtLayoutSource.DropCapPunctuation(XElement.Parse(Settings(false))).ShouldBeFalse();

        // Without `xmlns:ooo` the set's name is not the OpenOffice.org configuration's, and nothing in it
        // is read.
        string undeclared = Settings(null).Replace(" xmlns:ooo=\"http://openoffice.org/2004/office\"", string.Empty, StringComparison.Ordinal);
        OdtLayoutSource.DropCapPunctuation(XElement.Parse(undeclared)).ShouldBeTrue();
    }

    private static List<DrawnWord> Draw(string fodt)
        => ParagraphFrameTests.Draw(Encoding.UTF8.GetBytes(fodt), "probe.fodt");

    private static List<DrawnWord> Draw(byte[] bytes, string name) => ParagraphFrameTests.Draw(bytes, name);

    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" "
        + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" "
        + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
        + "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" "
        + "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" "
        + "xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" "
        + "xmlns:ooo=\"http://openoffice.org/2004/office\"";

    private const string FontFaces =
        "<office:font-face-decls><style:font-face style:name=\"Liberation Serif\" "
        + "svg:font-family=\"'Liberation Serif'\"/></office:font-face-decls>";

    private const string Styles =
        "<office:styles><style:default-style style:family=\"paragraph\">"
        + "<style:paragraph-properties fo:margin-top=\"0cm\" fo:margin-bottom=\"0cm\"/>"
        + "<style:text-properties style:font-name=\"Liberation Serif\" fo:font-size=\"12pt\" fo:language=\"en\" "
        + "fo:country=\"US\"/></style:default-style><style:style style:name=\"Standard\" style:family=\"paragraph\"/>"
        + "</office:styles>";

    private static string AutomaticStyles(string dropCap)
        => "<office:automatic-styles><style:page-layout style:name=\"pm1\"><style:page-layout-properties "
           + "fo:page-width=\"21.001cm\" fo:page-height=\"29.7cm\" fo:margin-top=\"2.54cm\" fo:margin-bottom=\"2.54cm\" "
           + "fo:margin-left=\"2.54cm\" fo:margin-right=\"2.54cm\"/></style:page-layout>"
           + "<style:style style:name=\"P1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\">"
           + $"<style:paragraph-properties><style:drop-cap {dropCap} style:distance=\"0cm\"/>"
           + "</style:paragraph-properties></style:style></office:automatic-styles>";

    private const string MasterStyles =
        "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\"/>"
        + "</office:master-styles>";

    private static string Body(string opening)
        => "<office:body><office:text><text:p text:style-name=\"Standard\">BEFORE text</text:p>"
           + $"<text:p text:style-name=\"P1\">{opening}{Lorem}</text:p>"
           + $"<text:p text:style-name=\"Standard\">SECOND {Lorem}</text:p></office:text></office:body>";

    private static string Fodt(string dropCap, string opening = "WAFTER ")
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
           + $"<office:document {Namespaces} office:version=\"1.3\" office:mimetype=\"application/vnd.oasis.opendocument.text\">"
           + FontFaces + Styles + AutomaticStyles(dropCap) + MasterStyles + Body(opening) + "</office:document>";

    private static string Settings(bool? dropCapPunctuation)
        => $"<office:document-settings {Namespaces} office:version=\"1.3\"><office:settings>"
           + "<config:config-item-set config:name=\"ooo:configuration-settings\">"
           + "<config:config-item config:name=\"AddExternalLeading\" config:type=\"boolean\">true</config:config-item>"
           + (dropCapPunctuation is { } value
               ? "<config:config-item config:name=\"DropCapPunctuation\" config:type=\"boolean\">"
                 + (value ? "true" : "false") + "</config:config-item>"
               : string.Empty)
           + "</config:config-item-set></office:settings></office:document-settings>";

    private static byte[] Odt(bool? dropCapPunctuation)
    {
        const string head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
        string content = head + $"<office:document-content {Namespaces} office:version=\"1.3\">"
                         + FontFaces + AutomaticStyles("style:lines=\"3\" style:length=\"1\"")
                         + Body("“WAFTER ") + "</office:document-content>";
        string styles = head + $"<office:document-styles {Namespaces} office:version=\"1.3\">"
                        + FontFaces + Styles
                        + "<office:automatic-styles><style:page-layout style:name=\"pm1\"><style:page-layout-properties "
                        + "fo:page-width=\"21.001cm\" fo:page-height=\"29.7cm\" fo:margin-top=\"2.54cm\" "
                        + "fo:margin-bottom=\"2.54cm\" fo:margin-left=\"2.54cm\" fo:margin-right=\"2.54cm\"/>"
                        + "</style:page-layout></office:automatic-styles>"
                        + MasterStyles + "</office:document-styles>";
        string manifest = head
                          + "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">"
                          + "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>"
                          + "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>"
                          + "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>"
                          + "<manifest:file-entry manifest:full-path=\"settings.xml\" manifest:media-type=\"text/xml\"/>"
                          + "</manifest:manifest>";

        using MemoryStream result = new();
        using (ZipArchive archive = new(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression);
            Write(archive, "content.xml", content);
            Write(archive, "styles.xml", styles);
            Write(archive, "settings.xml", head + Settings(dropCapPunctuation));
            Write(archive, "META-INF/manifest.xml", manifest);
        }

        return result.ToArray();

        static void Write(
            ZipArchive archive, string name, string text, CompressionLevel level = CompressionLevel.Optimal)
        {
            using Stream entry = archive.CreateEntry(name, level).Open();
            entry.Write(Encoding.UTF8.GetBytes(text));
        }
    }
}
