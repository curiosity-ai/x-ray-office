using System.IO.Compression;
using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Globalization;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A paragraph with automatic hyphenation on breaks its lines where LibreOffice 26.8.1.1 does, in RTF, DOCX
/// and ODF, and one with it off — or in capitals where the document says so — is unchanged.
/// </summary>
/// <remarks>
/// <para>
/// Every expected line is 26.8.1.1's own rendering of the same document, read with <c>pdftotext</c>: a
/// 3.5 inch page with half-inch margins, 12 pt Liberation Serif in RTF and ODF and Word's default size in
/// DOCX. Each reader turns its own spelling into one <see cref="Text.Layout.HyphenationRules"/>: RTF's
/// <c>\hyphauto</c>, <c>\hyphpar0</c> and <c>\hyphcaps0</c>; DOCX's <c>w:autoHyphenation</c> and
/// <c>w:suppressAutoHyphens</c>; ODF's <c>fo:hyphenate</c> with its remain and push counts. writerfilter
/// gives RTF and DOCX a 0.25 inch hyphenation zone (<c>SettingsTable.cxx</c>:591-606), which is what makes
/// <c>organisational</c> end its line whole in the RTF and not in the ODF.
/// </para>
/// <para>
/// Only English is hyphenated, because <c>hyph_en_US.dic</c> is the one pattern file shipped; the tests
/// skip when it is not beside the assembly.
/// </para>
/// </remarks>
public sealed class AutomaticHyphenationLayoutTests
{
    private const string Text =
        "Characterisation of internationalisation requirements demonstrates comprehensive understanding. "
        + "Notwithstanding considerable organisational difficulties, the administration implemented extraordinary "
        + "telecommunications infrastructure, transforming responsibilities fundamentally. "
        + "Unquestionably, establishment hyphenation algorithms accommodate unbelievably sophisticated vocabularies.";

    private const string Caps =
        "INTERNATIONALISATION CHARACTERISATION ADMINISTRATION TELECOMMUNICATIONS ESTABLISHMENT REQUIREMENTS";

    private static readonly string[] Unhyphenated =
    [
        "Characterisation of", "internationalisation requirements", "demonstrates comprehensive",
        "understanding. Notwithstanding", "considerable organisational", "difficulties, the administration",
        "implemented extraordinary", "telecommunications infrastructure,", "transforming responsibilities",
        "fundamentally. Unquestionably,", "establishment hyphenation", "algorithms accommodate",
        "unbelievably sophisticated", "vocabularies.",
    ];

    [Fact]
    public void AnRtfParagraphHyphenatesUnlessItsParagraphSaysOtherwise()
    {
        string rtf = Rtf(15840, @"\hyphauto", ("", Text), (@"\hyphpar0", Text), ("", Caps));

        string[] expected =
        [
            "Characterisation of internationalisa-", "tion requirements demonstrates com-",
            "prehensive understanding. Notwith-", "standing considerable organisational",
            "difficulties, the administration imple-", "mented extraordinary telecommuni-",
            "cations infrastructure, transforming", "responsibilities fundamentally. Un-",
            "questionably, establishment hyphen-", "ation algorithms accommodate unbe-",
            "lievably sophisticated vocabularies.",
            .. Unhyphenated,
            "INTERNATIONALISATION", "CHARACTERISATION ADMINIS-", "TRATION TELECOMMUNICA-",
            "TIONS ESTABLISHMENT RE-", "QUIREMENTS",
        ];

        Pages(Encoding.ASCII.GetBytes(rtf), "probe.rtf")[0].ShouldBe(expected);
    }

    [Fact]
    public void AnRtfDocumentThatForbidsCapitalsLeavesThemWhole()
    {
        string rtf = Rtf(15840, @"\hyphauto\hyphcaps0", ("", Caps));

        Pages(Encoding.ASCII.GetBytes(rtf), "probe.rtf")[0].ShouldBe(
            ["INTERNATIONALISATION", "CHARACTERISATION", "ADMINISTRATION", "TELECOMMUNICATIONS",
             "ESTABLISHMENT", "REQUIREMENTS"]);
    }

    [Fact]
    public void ADocxParagraphHyphenatesUnlessItSuppressesIt()
    {
        string[] expected =
        [
            "Characterisation of internationalisation", "requirements demonstrates comprehen-",
            "sive understanding. Notwithstanding", "considerable organisational difficulties,",
            "the administration implemented extraor-", "dinary telecommunications infrastruc-",
            "ture, transforming responsibilities fun-", "damentally. Unquestionably, establish-",
            "ment hyphenation algorithms accommo-", "date unbelievably sophisticated vocabu-",
            "laries.",
            "Characterisation of internationalisation", "requirements demonstrates",
            "comprehensive understanding.", "Notwithstanding considerable", "organisational difficulties, the",
            "administration implemented", "extraordinary telecommunications", "infrastructure, transforming",
            "responsibilities fundamentally.", "Unquestionably, establishment",
            "hyphenation algorithms accommodate", "unbelievably sophisticated vocabularies.",
            "INTERNATIONALISATION CHAR-", "ACTERISATION ADMINISTRATION", "TELECOMMUNICATIONS ESTAB-",
            "LISHMENT REQUIREMENTS",
        ];

        byte[] docx = Docx(
            "<w:autoHyphenation/>",
            ("", Text), ("<w:suppressAutoHyphens/>", Text), ("", Caps));

        Pages(docx, "probe.docx")[0].ShouldBe(expected);
    }

    [Fact]
    public void AnOdfParagraphHyphenatesWithItsOwnCounts()
    {
        // Four characters on either side of the hyphen: `organ-`, `administra-` and `funda-` survive, and
        // `com-`, `Notwith-` and `Un-` do not. No zone in ODF, so `organisational` is broken too.
        string fodt = Fodt(
            """
            <style:style style:name="H4" style:family="paragraph"><style:text-properties fo:hyphenate="true" fo:hyphenation-remain-char-count="4" fo:hyphenation-push-char-count="4"/></style:style>
            <style:style style:name="N" style:family="paragraph"/>
            """,
            ("H4", Text), ("N", Text));

        string[] expected =
        [
            "Characterisation of internationalisa-", "tion requirements demonstrates",
            "comprehensive understanding.", "Notwithstanding considerable organ-",
            "isational difficulties, the administra-", "tion implemented extraordinary",
            "telecommunications infrastructure,", "transforming responsibilities funda-",
            "mentally. Unquestionably, establish-", "ment hyphenation algorithms accom-",
            "modate unbelievably sophisticated", "vocabularies.",
            .. Unhyphenated,
        ];

        Pages(Encoding.UTF8.GetBytes(fodt), "probe.fodt")[0].ShouldBe(expected);
    }

    [Fact]
    public void TheLineThatEndsAPageIsNotHyphenated()
    {
        // writerfilter's hyphenation-keep (`SettingsTable.cxx`:780-785): 26.8.1.1 ends page 1 on
        // "difficulties, the administration" where the same paragraph on a tall page reads
        // "difficulties, the administration imple-", and page 2 then opens on the whole word.
        string rtf = Rtf(3000, @"\hyphauto", ("", Text + " " + Text + " " + Text));

        List<List<string>> pages = Pages(Encoding.ASCII.GetBytes(rtf), "probe.rtf");

        pages[0].ShouldBe(
            ["Characterisation of internationalisa-", "tion requirements demonstrates com-",
             "prehensive understanding. Notwith-", "standing considerable organisational",
             "difficulties, the administration"]);
        pages[1][0].ShouldBe("implemented extraordinary telecom-");
    }

    private static List<List<string>> Pages(byte[] bytes, string name)
    {
        Assert.SkipWhen(Hyphenators.For("en-US") is null, "hyph_en_US.dic is not beside the assembly");

        RecordingDrawingSink sink = new();

        using (DocumentSource source = DocumentSource.FromStream(new MemoryStream(bytes), name))
        {
            using IDocument read = new WordProcessingReader().Read(source);
            IPageSequence pages = ((IPaginatedDocument)read).Layout();
            for (int index = 0; index < pages.Count; index++) pages[index].Draw(sink);
        }

        return sink.Pages
            .Select(page => DrawnWords.On(page)
                .GroupBy(word => Math.Round(word.Baseline, 1))
                .OrderBy(line => line.Key)
                .Select(line => string.Join(' ', line.OrderBy(word => word.Left).Select(word => word.Text)))
                .ToList())
            .ToList();
    }

    private static string Rtf(int paperHeight, string header, params (string Words, string Text)[] paragraphs)
    {
        StringBuilder rtf = new();
        rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\froman Liberation Serif;}}\paperw5040\paperh")
           .Append(paperHeight)
           .Append(@"\margl720\margr720\margt720\margb720")
           .Append(header)
           .Append('\n');

        foreach ((string words, string text) in paragraphs)
            rtf.Append(@"{\pard").Append(words).Append(' ').Append(text).Append(@"\par}").Append('\n');

        return rtf.Append('}').ToString();
    }

    private static byte[] Docx(string settings, params (string Properties, string Text)[] paragraphs)
    {
        const string w = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

        StringBuilder body = new();
        foreach ((string properties, string text) in paragraphs)
        {
            body.Append("<w:p><w:pPr>").Append(properties).Append("</w:pPr><w:r><w:rPr>")
                .Append("<w:rFonts w:ascii=\"Liberation Serif\" w:hAnsi=\"Liberation Serif\"/></w:rPr>")
                .Append("<w:t xml:space=\"preserve\">").Append(text).Append("</w:t></w:r></w:p>");
        }

        string document =
            $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document {w}><w:body>{body}"
            + "<w:sectPr><w:pgSz w:w=\"5040\" w:h=\"15840\"/><w:pgMar w:top=\"720\" w:right=\"720\" "
            + "w:bottom=\"720\" w:left=\"720\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr>"
            + "</w:body></w:document>";

        using MemoryStream result = new();
        using (ZipArchive archive = new(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
                + "<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/></Types>");
            Write(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Write(archive, "word/_rels/document.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings\" Target=\"settings.xml\"/></Relationships>");
            Write(archive, "word/document.xml", document);
            Write(archive, "word/settings.xml",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:settings {w}>{settings}</w:settings>");
        }

        return result.ToArray();

        static void Write(ZipArchive archive, string name, string content)
        {
            using Stream entry = archive.CreateEntry(name).Open();
            entry.Write(Encoding.UTF8.GetBytes(content));
        }
    }

    private static string Fodt(string styles, params (string Style, string Text)[] paragraphs)
    {
        string body = string.Concat(paragraphs.Select(p => $"<text:p text:style-name=\"{p.Style}\">{p.Text}</text:p>"));

        return
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:loext="urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
            <office:font-face-decls><style:font-face style:name="Liberation Serif" svg:font-family="'Liberation Serif'" style:font-family-generic="roman"/></office:font-face-decls>
            <office:styles><style:default-style style:family="paragraph"><style:text-properties style:font-name="Liberation Serif" fo:font-size="12pt" fo:language="en" fo:country="US"/></style:default-style>
            """
            + styles
            + """
              </office:styles>
              <office:automatic-styles><style:page-layout style:name="pm1"><style:page-layout-properties fo:page-width="3.5in" fo:page-height="11in" fo:margin-top="0.5in" fo:margin-bottom="0.5in" fo:margin-left="0.5in" fo:margin-right="0.5in"/></style:page-layout></office:automatic-styles>
              <office:master-styles><style:master-page style:name="Standard" style:page-layout-name="pm1"/></office:master-styles>
              <office:body><office:text>
              """
            + body
            + "</office:text></office:body></office:document>";
    }
}
