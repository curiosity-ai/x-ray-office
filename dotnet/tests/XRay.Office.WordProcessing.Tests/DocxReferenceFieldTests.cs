using XRay.Office.Core.Documents;
using XRay.Office.TestKit;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A DOCX <c>REF</c> field draws the result the producer cached, as Word does — and 26.2.4.2
/// recomputes it from the bookmark.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The two disagree about <em>when</em> a field is evaluated, not about what it evaluates
/// to.</strong> Word does not update a <c>REF</c> when a document is opened or printed: the reader
/// sees what was last computed, and only F9 or an explicit *update fields before printing* changes
/// it. LibreOffice recomputes every one on load — a <c>REF</c> naming a bookmark becomes a
/// <c>SwGetRefField</c> with <c>ReferenceFieldSource::BOOKMARK</c>
/// (<c>dmapper/DomainMapper_Impl.cxx</c>:8541-8635) and <c>UpdateField</c> reads the bookmark's own
/// text through <c>FindAnchor</c> (<c>reffld.cxx</c>:1559-1591, whose <b>−1</b> for two halves in
/// different nodes <c>:603-607</c> reads as <em>to the end of the paragraph</em>), filtered by
/// <c>FilterText</c> (<c>:461-489</c>).
/// </para>
/// <para>
/// This tree follows Word, so the tests below assert the cache, on the committed fixture
/// <c>tests/corpus/features/words-reference-field.docx</c>, one arm per paragraph, each field
/// caching a deliberately wrong result so that what the reference draws is its own answer. There is
/// no way to ask for LibreOffice's reading instead; what it draws on each arm is recorded in
/// <c>probes/docxref-r158/reference-arms.txt</c>.
/// </para>
/// <para>
/// What following Word costs is in <c>dotnet/TODO.word-parity.md</c>. It is visible on
/// <c>FAA 2025-26 Holdover Tables.docx</c>, where 26.2.4.2 draws the nonsense its own rule produces
/// — <c>The list of fluids (Tables Table 55, Table 56, Table 57 and Table 58)</c> against the file's
/// cached <c>(Tables 55, 56, 57 and 58)</c> — and one note's expansion is a whole page of that
/// document.
/// </para>
/// </remarks>
public sealed class DocxReferenceFieldTests
{
    private const string Fixture = "words-reference-field.docx";

    /// <summary>
    /// A bookmark covering more than the cache does not change what is drawn.
    /// </summary>
    /// <remarks>
    /// Arm A: the mark covers <c>Table 7</c> across two runs and the field cached <c>7</c>. Word
    /// draws <c>7</c> until the field is updated; 26.2.4.2 draws <c>Table 7</c>.
    /// </remarks>
    [Fact]
    public void TheCachedResultIsDrawn() => Arm("A more than the cache").ShouldBe("7 .");

    /// <summary>And a deliberately stale cache is drawn stale.</summary>
    /// <remarks>
    /// Arm B, whose bookmark ends in another paragraph. The reference draws <c>half one</c>; the
    /// file says <c>stale</c> and so do we.
    /// </remarks>
    [Fact]
    public void AStaleCacheIsNotCorrected() => Arm("B into the next paragraph").ShouldBe("stale .");

    /// <summary>
    /// The compact <c>w:fldSimple</c> spelling is the same field and is treated the same way.
    /// </summary>
    /// <remarks>Arm L, which states its instruction as an attribute and its cache as its children.</remarks>
    [Fact]
    public void TheCompactFormIsDrawnFromItsCacheToo()
        => Arm("L the compact form").ShouldBe("stale .");

    /// <summary>The instruction reader, on which arguments name a bookmark at all.</summary>
    [Fact]
    public void TheInstructionReaderNamesTheBookmarkOnlyWhenTheTextIsWhatIsQuoted()
    {
        FieldInstructions.ReferenceBookmark(" REF _Ref1 \\h ").ShouldBe("_Ref1");
        FieldInstructions.ReferenceBookmark(" REF _Ref1 \\h \\* MERGEFORMAT ").ShouldBe("_Ref1");
        FieldInstructions.ReferenceBookmark(" REF \"a name\" \\h ").ShouldBe("a name");
        FieldInstructions.ReferenceBookmark(" REF _Ref1 \\p ").ShouldBeNull();
        FieldInstructions.ReferenceBookmark(" REF _Ref1 \\n ").ShouldBeNull();
        FieldInstructions.ReferenceBookmark(" PAGEREF _Ref1 \\h ").ShouldBeNull();
        FieldInstructions.ReferenceBookmark(" REF ").ShouldBeNull();
    }

    /// <summary>What the fixture's arm of that label lays out, after the label and its colon.</summary>
    private static string Arm(string label)
    {
        string line = Lines().FirstOrDefault(text => text.StartsWith(label + ": ", StringComparison.Ordinal))
                      ?? throw new InvalidOperationException($"the fixture states no arm '{label}'");

        return line[(label.Length + 2)..];
    }

    private static List<string> Lines()
    {
        using DocumentSource source = DocumentSource.FromFile(Corpus.Require(Fixture));
        using IDocument document = new WordProcessingReader().Read(source);

        var pages = (WordProcessingPages)((IPaginatedDocument)document).Layout();
        return [.. pages.Paragraphs.Select(paragraph => paragraph.Text)];
    }
}
