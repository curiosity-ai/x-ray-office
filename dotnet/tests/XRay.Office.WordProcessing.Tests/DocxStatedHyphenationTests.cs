using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// <c>w:suppressAutoHyphens w:val="0"</c> hyphenates its paragraph even in a document without
/// <c>w:autoHyphenation</c>: dmapper maps it to <c>ParaIsHyphenation = !value</c> whatever the settings say.
/// </summary>
/// <remarks>
/// Measured on 26.8's daily: one paragraph stating it in a document with no settings at all draws two
/// hyphenated lines, and this tree now draws the same five lines. The zone and the keep come only with the
/// document settings (<c>SettingsTable.cxx</c>:751-752), so the paragraph takes the pool's rules.
/// <c>docx/issue_359_list_whitespace.docx</c> states it six times under <c>w:autoHyphenation w:val="0"</c>.
/// </remarks>
public sealed class DocxStatedHyphenationTests
{
    [Theory]
    [InlineData("""<w:suppressAutoHyphens w:val="0"/>""", "", true)]
    [InlineData("""<w:suppressAutoHyphens/>""", "", false)]
    [InlineData("", "", false)]
    [InlineData("", "<w:autoHyphenation/>", true)]
    [InlineData("""<w:suppressAutoHyphens/>""", "<w:autoHyphenation/>", false)]
    public void AParagraphCanTurnHyphenationOnItself(string pPr, string settings, bool hyphenates)
        => (Format(pPr, settings).Hyphenation is not null).ShouldBe(hyphenates);

    [Fact]
    public void WithoutTheDocumentSettingItTakesThePoolsRules()
        => Format("""<w:suppressAutoHyphens w:val="0"/>""", "").Hyphenation.ShouldBe(HyphenationRules.Default);

    private static ParagraphFormat Format(string pPr, string settings)
        => MiniDocx.Layout($"<w:p><w:pPr>{pPr}</w:pPr><w:r><w:t>Words</w:t></w:r></w:p>", settings: settings)
            .Blocks.OfType<PageParagraph>().First().Format;
}
