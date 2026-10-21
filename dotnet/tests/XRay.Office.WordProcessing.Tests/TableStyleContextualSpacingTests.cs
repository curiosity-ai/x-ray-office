using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A table style's own <c>w:contextualSpacing</c> reaches the paragraphs in its cells, as it does in
/// Word — a deliberate divergence from 26.8.1.1, which drops it.
/// </summary>
/// <remarks>
/// 26.8.1.1 keeps the 12 pt between three same-style cell paragraphs whose table style states only
/// <c>&lt;w:pPr&gt;&lt;w:contextualSpacing/&gt;&lt;/w:pPr&gt;</c>, because <c>PROP_PARA_CONTEXT_MARGIN</c> sat
/// outside the range <c>isParagraphProperty</c> tests; upstream fixed it after the 26.8 branch
/// (<c>5dc72823c12d</c>). <c>TODO.word-parity.md</c> records it, with the switch that reproduces the
/// reference; <c>probes/words-tblctx-r268/</c> the measurement.
/// </remarks>
public sealed class TableStyleContextualSpacingTests
{
    /// <summary>By default the cell's same-style paragraphs lose the space between them.</summary>
    [Fact]
    public void ATableStylesContextualSpacingReachesItsCells()
    {
        const string Styles = """
            <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
            <w:style w:type="paragraph" w:styleId="Body"><w:name w:val="Body"/>
              <w:pPr><w:spacing w:before="0" w:after="240"/></w:pPr></w:style>
            <w:style w:type="table" w:styleId="Ctx"><w:name w:val="Ctx"/>
              <w:pPr><w:contextualSpacing/></w:pPr></w:style>
            """;

        const string Body = """
            <w:tbl><w:tblPr><w:tblStyle w:val="Ctx"/><w:tblW w:w="5000" w:type="dxa"/></w:tblPr>
              <w:tblGrid><w:gridCol w:w="5000"/></w:tblGrid>
              <w:tr><w:tc>
                <w:p><w:pPr><w:pStyle w:val="Body"/></w:pPr><w:r><w:t>first</w:t></w:r></w:p>
                <w:p><w:pPr><w:pStyle w:val="Body"/></w:pPr><w:r><w:t>second</w:t></w:r></w:p>
              </w:tc></w:tr></w:tbl>
            <w:p><w:pPr><w:pStyle w:val="Body"/></w:pPr><w:r><w:t>after</w:t></w:r></w:p>
            """;

        List<PageParagraph> cell = [.. MiniDocx.Layout(Body, Styles).Pages[0].Tables
            .SelectMany(table => table.Cells)
            .SelectMany(placed => placed.Content!.Blocks).OfType<PageParagraph>()];

        cell.Count.ShouldBe(2);
        cell.ShouldAllBe(paragraph => paragraph.Format.HasContextualSpacing);
    }
}
