using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// Three things Writer's ODF import does that a document from another producer exposes and one LibreOffice
/// wrote never does: a cell with no stated padding has none, a percentage font size on a named style is of
/// its parent and on an automatic style is ignored, and character data is white-space collapsed.
/// </summary>
/// <remarks>
/// Every expectation is 26.8.1.1's own rendering of the same flat document, read with
/// <c>pdftotext -bbox</c> and <c>pdf-ops.py</c>; 26.2.4.2 renders the size probe identically. The witness that
/// needed all three is pandoc's <c>odt/extraction_test.odt</c>: page 1 now ends on the same line as the
/// reference's and its footer's page number sits on the reference's baseline.
/// </remarks>
public sealed class OdtImportDefaultsTests
{
    private const double Tolerance = 0.2;

    private const string Head =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
        <office:font-face-decls><style:font-face style:name="Liberation Serif" svg:font-family="'Liberation Serif'" style:font-family-generic="roman"/></office:font-face-decls>
        """;

    /// <summary>
    /// A cell whose style states no padding has none; one that states Writer's 0.097 cm has that.
    /// </summary>
    /// <remarks>
    /// The 55 twips are what a table inserted through the user interface gets, not what the import gives
    /// (<c>sw/source/core/docnode/ndtbl.cxx</c>:117-147). 26.8.1.1 puts the first cell's text 0.00 pt right
    /// of the paragraph after the table with no padding stated, and 2.75 pt with it.
    /// </remarks>
    [Theory]
    [InlineData("", 0.0)]
    [InlineData("<style:table-cell-properties fo:border=\"none\"/>", 0.0)]
    [InlineData("<style:table-cell-properties fo:padding=\"0.097cm\" fo:border=\"none\"/>", 2.75)]
    public void ACellPadsOnlyWhatItsStyleStates(string properties, double inset)
    {
        string document = Head
            + """
              <office:automatic-styles>
              <style:style style:name="T" style:family="table"><style:table-properties style:width="1.2in" table:align="left"/></style:style>
              <style:style style:name="C" style:family="table-column"><style:table-column-properties style:column-width="0.6in"/></style:style>
              <style:style style:name="X" style:family="table-cell">
              """
            + properties
            + """
              </style:style>
              </office:automatic-styles>
              <office:master-styles><style:master-page style:name="Standard"/></office:master-styles>
              <office:body><office:text>
              <table:table table:name="t" table:style-name="T"><table:table-column table:style-name="C" table:number-columns-repeated="2"/>
              <table:table-row><table:table-cell table:style-name="X"><text:p>CELL</text:p></table:table-cell><table:table-cell table:style-name="X"><text:p>B</text:p></table:table-cell></table:table-row>
              </table:table>
              <text:p>AFTER</text:p>
              </office:text></office:body></office:document>
              """;

        Dictionary<string, DrawnWord> words = Words(document);

        (words["CELL"].Left - words["AFTER"].Left).ShouldBe(inset, Tolerance);
    }

    /// <summary>
    /// A named style's percentage is of its parent, whatever order the two are declared in, and is inherited
    /// as a size; an automatic style's is ignored.
    /// </summary>
    [Theory]
    [InlineData("NAMED115", 16.1)]   // 115 % of a 14 pt parent declared after it
    [InlineData("NAMED150", 30.0)]   // 150 % of a 20 pt parent declared before it
    [InlineData("CHAIN120", 19.3)]   // 120 % of that 16.1, truncated to whole twips
    [InlineData("INHERITS", 16.1)]   // a child stating nothing takes 16.1 as a size
    [InlineData("NOPARENT", 18.0)]   // 150 % of the document default
    [InlineData("AUTO115", 14.0)]    // an automatic 115 % over the 14 pt parent: the parent's size
    [InlineData("SPAN150", 11.0)]    // an automatic 150 % span in an 11 pt paragraph: the paragraph's
    [InlineData("CHARSTYLE", 18.0)]  // a named character style's 150 % is of its own chain, not the paragraph's
    public void APercentageIsOfTheNamedParentAndIgnoredOnAnAutomaticStyle(string word, double size)
    {
        string document = Head
            + """
              <office:styles>
              <style:default-style style:family="paragraph"><style:text-properties style:font-name="Liberation Serif" fo:font-size="12pt"/></style:default-style>
              <style:style style:name="NP115" style:family="paragraph" style:parent-style-name="H14"><style:text-properties fo:font-size="115%"/></style:style>
              <style:style style:name="H14" style:family="paragraph"><style:text-properties fo:font-size="14pt"/></style:style>
              <style:style style:name="H20" style:family="paragraph"><style:text-properties fo:font-size="20pt"/></style:style>
              <style:style style:name="NP150" style:family="paragraph" style:parent-style-name="H20"><style:text-properties fo:font-size="150%"/></style:style>
              <style:style style:name="Chain" style:family="paragraph" style:parent-style-name="NP115"><style:text-properties fo:font-size="120%"/></style:style>
              <style:style style:name="Kid" style:family="paragraph" style:parent-style-name="NP115"/>
              <style:style style:name="Orphan" style:family="paragraph"><style:text-properties fo:font-size="150%"/></style:style>
              <style:style style:name="P11" style:family="paragraph"><style:text-properties fo:font-size="11pt"/></style:style>
              <style:style style:name="C150" style:family="text"><style:text-properties fo:font-size="150%"/></style:style>
              </office:styles>
              <office:automatic-styles>
              <style:style style:name="A115" style:family="paragraph" style:parent-style-name="H14"><style:text-properties fo:font-size="115%"/></style:style>
              <style:style style:name="T150" style:family="text"><style:text-properties fo:font-size="150%"/></style:style>
              </office:automatic-styles>
              <office:master-styles><style:master-page style:name="Standard"/></office:master-styles>
              <office:body><office:text>
              <text:p text:style-name="NP115">NAMED115</text:p>
              <text:p text:style-name="NP150">NAMED150</text:p>
              <text:p text:style-name="Chain">CHAIN120</text:p>
              <text:p text:style-name="Kid">INHERITS</text:p>
              <text:p text:style-name="Orphan">NOPARENT</text:p>
              <text:p text:style-name="A115">AUTO115</text:p>
              <text:p text:style-name="P11">x <text:span text:style-name="T150">SPAN150</text:span></text:p>
              <text:p text:style-name="H20">x <text:span text:style-name="C150">CHARSTYLE</text:span></text:p>
              </office:text></office:body></office:document>
              """;

        Words(document)[word].Size.ShouldBe(size, 0.05);
    }

    /// <summary>
    /// White space in character data collapses to one space and is dropped at the paragraph's start, so a
    /// pretty-printed paragraph is one line; and a page-number field's cached text is only its digits.
    /// </summary>
    /// <remarks>
    /// 26.8.1.1 draws <c>A␣␣⏎␣␣B</c> as <c>A B</c> on one line, and pandoc's footer — a newline and
    /// indentation either side of a field whose cached value is a newline, ten spaces and <c>1</c> — as one
    /// line holding the number. Before, the newlines were line breaks: both footers were a line taller.
    /// </remarks>
    [Fact]
    public void CharacterDataIsWhiteSpaceCollapsed()
    {
        string document = Head
            + """
              <office:automatic-styles/>
              <office:master-styles><style:master-page style:name="Standard"/></office:master-styles>
              <office:body><office:text>
              <text:p>
                 FIRST

                 SECOND</text:p>
              <text:p>
                      <text:page-number text:select-page="current">
                      7</text:page-number>
                    LAST</text:p>
              </office:text></office:body></office:document>
              """;

        Dictionary<string, DrawnWord> words = Words(document);

        words["SECOND"].Baseline.ShouldBe(words["FIRST"].Baseline, Tolerance);
        (words["LAST"].Baseline - words["SECOND"].Baseline).ShouldBeLessThan(20.0);
        (words["SECOND"].Left - words["FIRST"].Right).ShouldBeLessThan(4.0);
    }

    private static Dictionary<string, DrawnWord> Words(string document)
    {
        RecordingDrawingSink sink = new();

        using (DocumentSource source = DocumentSource.FromStream(
                   new MemoryStream(Encoding.UTF8.GetBytes(document)), "probe.fodt"))
        {
            using IDocument read = new WordProcessingReader().Read(source);
            IPageSequence pages = ((IPaginatedDocument)read).Layout();
            pages[0].Draw(sink);
        }

        return DrawnWords.On(sink.Pages[0])
            .GroupBy(word => word.Text)
            .ToDictionary(group => group.Key, group => group.First());
    }
}
