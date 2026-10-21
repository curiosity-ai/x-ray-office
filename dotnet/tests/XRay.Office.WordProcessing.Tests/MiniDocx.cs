using System.IO.Compression;
using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.WordProcessing.Layout;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A DOCX built in memory from a body, a styles part and a settings part, for tests that need one rule
/// in isolation and nothing else from a real document.
/// </summary>
/// <remarks>
/// The settings part is always written, even empty: without one a hand-built DOCX misses LibreOffice's
/// OOXML compatibility defaults, and several synthetics built that way have given clean, consistent,
/// wrong answers.
/// </remarks>
internal static class MiniDocx
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Builds the package and lays it out.</summary>
    /// <param name="body">The children of <c>w:body</c>, without the <c>w:sectPr</c>.</param>
    /// <param name="styles">The children of <c>w:styles</c>.</param>
    /// <param name="settings">The children of <c>w:settings</c>.</param>
    /// <param name="sectionProperties">The children of the body's <c>w:sectPr</c>, or null for Letter with
    /// one-inch margins.</param>
    /// <param name="numbering">The children of <c>w:numbering</c>, or null for no numbering part.</param>
    public static WordProcessingPages Layout(
        string body, string styles = "", string settings = "", string? sectionProperties = null,
        string? numbering = null)
    {
        using MemoryStream package = Build(body, styles, settings, sectionProperties, numbering);
        using DocumentSource source = DocumentSource.FromStream(package, "mini.docx");
        using IDocument document = new WordProcessingReader().Read(source);
        return (WordProcessingPages)((IPaginatedDocument)document).Layout();
    }

    /// <summary>Builds the package.</summary>
    public static MemoryStream Build(
        string body, string styles = "", string settings = "", string? sectionProperties = null,
        string? numbering = null)
    {
        const string ContentTypes = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml"
                        ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
              <Override PartName="/word/styles.xml"
                        ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
              <Override PartName="/word/settings.xml"
                        ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
              <Override PartName="/word/numbering.xml"
                        ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
            </Types>
            """;

        const string RootRelationships = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Target="word/document.xml"
                            Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"/>
            </Relationships>
            """;

        const string DocumentRelationships = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Target="styles.xml"
                            Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"/>
              <Relationship Id="rId2" Target="settings.xml"
                            Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings"/>
              <Relationship Id="rId3" Target="numbering.xml"
                            Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering"/>
            </Relationships>
            """;

        string section = sectionProperties
            ?? """
               <w:pgSz w:w="12240" w:h="15840"/>
               <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"
                        w:header="720" w:footer="720" w:gutter="0"/>
               """;

        MemoryStream result = new();
        using (ZipArchive archive = new(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes);
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "word/_rels/document.xml.rels", DocumentRelationships);
            Write(archive, "word/settings.xml",
                  $"""<?xml version="1.0" encoding="UTF-8"?><w:settings xmlns:w="{W}">{settings}</w:settings>""");
            Write(archive, "word/styles.xml",
                  $"""<?xml version="1.0" encoding="UTF-8"?><w:styles xmlns:w="{W}">{styles}</w:styles>""");
            if (numbering is not null)
            {
                Write(archive, "word/numbering.xml",
                      $"""<?xml version="1.0" encoding="UTF-8"?><w:numbering xmlns:w="{W}">{numbering}</w:numbering>""");
            }

            Write(archive, "word/document.xml",
                  $"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="{W}"><w:body>{body}<w:sectPr>{section}</w:sectPr></w:body></w:document>""");
        }

        result.Position = 0;
        return result;

        static void Write(ZipArchive archive, string name, string content)
        {
            using Stream entry = archive.CreateEntry(name).Open();
            entry.Write(Encoding.UTF8.GetBytes(content));
        }
    }
}
