using XRay.Office.Core.Documents;
using XRay.Office.Core.Units;
using XRay.Office.TestKit;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// 26.2.4.2 voids the direct paragraph formatting of every paragraph in a built-in
/// <c>heading N</c> that a <c>TOC \t</c> switch names — and this tree deliberately does not.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Word honours the paragraph, so by default this tree does too.</strong> A <c>TOC</c>
/// carrying <c>\t "Heading 2,1,Heading 3,2"</c> registers those styles as the index's source styles
/// (<c>DomainMapper_Impl::handleToc</c>, <c>DomainMapper_Impl.cxx</c>:7663-7707), and from then on
/// 26.2.4.2 imports every paragraph in one of them, anywhere in the document, with its <c>w:pPr</c>
/// beyond the <c>w:pStyle</c> discarded. That is a quirk of one importer rather than anything the
/// format says, and reproducing it means drawing a document differently from the application that
/// wrote it, so this tree does not, and LibreOffice's reading is not available here.
/// <c>dotnet/TODO.word-parity.md</c> records what that costs on the corpus.
/// </para>
/// <para>
/// The tests assert what this tree draws — the paragraph's own formatting — on the committed fixture
/// <c>tests/corpus/features/words-toc-template-styles.docx</c>. The reference's answer for each arm,
/// read out of 26.2.4.2's own <c>--convert-to fodt</c>, is in
/// <c>probes/tocstyle-r160/reference-arms.txt</c>: five arms voided, and the four controls
/// (<c>heading 4</c>, a <c>w:customStyle</c> named <c>heading 5</c>, a <c>w:customStyle</c> with an
/// outline level, the built-in <c>Title</c>) untouched.
/// </para>
/// <para>
/// Three further controls could not live in that fixture, because the registration is document-wide
/// and its arms would void them: a <c>\t</c> naming only <c>Heading 2</c>, a <c>\o</c> switch with
/// no <c>\t</c>, and a document with no <c>TOC</c> at all. Each is its own file under
/// <c>probes/tocstyle-r160/</c> and each keeps its direct formatting in the reference too.
/// </para>
/// </remarks>
public sealed class DocxTocTemplateStyleTests
{
    private const string Fixture = "words-toc-template-styles.docx";

    /// <summary>The 6 pt the fixture's styles state and whose arms state 0.</summary>
    private static readonly Length StyleSpacing = Length.FromTwips(120);

    /// <summary>
    /// A paragraph in a named heading keeps the spacing it states, which is Word's answer.
    /// </summary>
    /// <remarks>
    /// Arm A, and the one that decides a page: the reference draws the style's 6 pt here, and on
    /// <c>24-25_FAA_Holdover_Tables.docx</c> those 6 pt are a whole page.
    /// </remarks>
    [Fact]
    public void ADirectSpacingIsHonoured() => Arm("A-named-spacing").SpaceAfter.ShouldBe(Length.Zero);

    /// <summary>And the alignment it states, over a style that centres.</summary>
    [Fact]
    public void ADirectAlignmentIsHonoured()
        => Arm("B-named-jc").Alignment.ShouldBe(TextAlignment.Start);

    /// <summary>And the indent.</summary>
    [Fact]
    public void ADirectIndentIsHonoured()
        => Arm("C-named-ind").StartIndent.ShouldBe(Length.FromTwips(720));

    /// <summary>A page break is kept either way, so this arm cannot tell the two apart.</summary>
    /// <remarks>
    /// Kept as a regression on the arm rather than on the rule: <c>w:pageBreakBefore</c> is the one
    /// property 26.2.4.2 does <em>not</em> discard, so a reader that got the exception backwards
    /// would show up here and nowhere else.
    /// </remarks>
    [Fact]
    public void APageBreakIsKept() => Arm("D-named-break").StartsNewPage.ShouldBeTrue();

    /// <summary>A paragraph in a heading the switch does not name is unaffected either way.</summary>
    [Fact]
    public void AnUnnamedHeadingKeepsItsFormatting()
        => Arm("G-other-style").SpaceAfter.ShouldBe(Length.Zero);

    private static ParagraphFormat Arm(string label)
    {
        using DocumentSource source = DocumentSource.FromFile(Corpus.Require(Fixture));
        using IDocument document = new WordProcessingReader().Read(source);

        var pages = (WordProcessingPages)((IPaginatedDocument)document).Layout();
        PageParagraph paragraph =
            pages.Paragraphs.FirstOrDefault(p => p.Text == label)
            ?? throw new InvalidOperationException($"the fixture states no arm '{label}'");

        return paragraph.DeclaredFormat;
    }
}
