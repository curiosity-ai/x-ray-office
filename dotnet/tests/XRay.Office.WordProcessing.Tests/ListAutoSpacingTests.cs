using XRay.Office.Core.Units;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// writerfilter's tdf#133363 rule: two paragraphs of one list lose the space between them when an
/// auto-spacing statement is anywhere in the predecessor's grab bag — even one that says <c>0</c>.
/// </summary>
/// <remarks>
/// <c>DomainMapper_Impl::finishParagraph</c> (<c>DomainMapper_Impl.cxx</c>:2768-2853). Measured on
/// <c>issue_359_list_whitespace.docx</c> against 26.8.1.1: list items at 159.41, 179.66, 199.91, 260.66
/// and 321.41 pt on both sides after the change, where this tree drew 159.41, 187.66, 215.91, 300.66 and
/// 385.41 — the 8 pt of the <c>Body</c> style's space before on every item after the first.
/// </remarks>
public sealed class ListAutoSpacingTests
{
    private static readonly Length Eight = Length.FromPoints(8);

    /// <summary>The defaults' <c>w:beforeAutospacing="0"</c> still removes the space between list items.</summary>
    [Fact]
    public void AnAutoSpacingStatementOfZeroStillRemovesTheSpace()
    {
        List<PageParagraph> paragraphs = Paragraphs(defaults: """w:beforeAutospacing="0" w:afterAutospacing="0" """);

        paragraphs[0].Format.SpaceBefore.ShouldBe(Eight, "the first item follows a paragraph with no list");
        paragraphs[1].Format.SpaceBefore.ShouldBe(Length.Zero);
        paragraphs[2].Format.SpaceBefore.ShouldBe(Length.Zero);
    }

    /// <summary>Without the statement the style's space stands.</summary>
    [Fact]
    public void WithoutTheStatementTheSpaceStands()
    {
        List<PageParagraph> paragraphs = Paragraphs(defaults: "");

        paragraphs[1].Format.SpaceBefore.ShouldBe(Eight);
        paragraphs[2].Format.SpaceBefore.ShouldBe(Eight);
    }

    /// <summary>A paragraph of a different list keeps its space.</summary>
    [Fact]
    public void ADifferentListKeepsItsSpace()
    {
        List<PageParagraph> paragraphs =
            Paragraphs(defaults: """w:beforeAutospacing="0" """, thirdNumId: 2);

        paragraphs[1].Format.SpaceBefore.ShouldBe(Length.Zero);
        paragraphs[2].Format.SpaceBefore.ShouldBe(Eight, "numId 2 is not the predecessor's list");
    }

    private static List<PageParagraph> Paragraphs(string defaults, int thirdNumId = 1)
    {
        string styles = $"""
            <w:docDefaults><w:pPrDefault><w:pPr><w:spacing w:before="0" w:after="0" {defaults}/></w:pPr></w:pPrDefault></w:docDefaults>
            <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
            <w:style w:type="paragraph" w:styleId="Body"><w:name w:val="Body"/>
              <w:pPr><w:spacing w:before="160" w:after="0"/></w:pPr></w:style>
            """;

        const string Numbering = """
            <w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/>
              <w:lvlText w:val="%1."/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr></w:lvl></w:abstractNum>
            <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
            <w:num w:numId="2"><w:abstractNumId w:val="0"/></w:num>
            """;

        string body = $"""
            <w:p><w:r><w:t>Lead</w:t></w:r></w:p>
            {Item(1, "One")}
            {Item(1, "Two")}
            {Item(thirdNumId, "Three")}
            """;

        return MiniDocx.Layout(body, styles, numbering: Numbering)
            .Blocks.OfType<PageParagraph>().Skip(1).Take(3).ToList();

        static string Item(int numId, string text)
            => $"""
                <w:p><w:pPr><w:pStyle w:val="Body"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="{numId}"/></w:numPr></w:pPr>
                  <w:r><w:t>{text}</w:t></w:r></w:p>
                """;
    }
}
