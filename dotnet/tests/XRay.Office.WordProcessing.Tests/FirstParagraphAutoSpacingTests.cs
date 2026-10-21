using XRay.Office.Core.Units;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// The HTML auto margin is not given to the first paragraph of a shape or of the document — the two arms
/// of <c>DomainMapper_Impl::finishParagraph</c>'s rule (<c>DomainMapper_Impl.cxx</c>:2458-2470) that sit
/// beside the table-cell one.
/// </summary>
/// <remarks>
/// Measured against 26.8.1.1: on an authored two-paragraph document both stating
/// <c>w:beforeAutospacing="1"</c> the first is set at the top margin, 72.03 pt, and the second 14 pt
/// below it, where this tree set the first at 86.03; and 26.8.1.1's flat export of <c>textbox.docx</c>
/// gives every box's first paragraph <c>fo:margin-top="0in"</c> and its later ones 0.1945 in.
/// </remarks>
public sealed class FirstParagraphAutoSpacingTests
{
    private const string Auto = """<w:pPr><w:spacing w:before="100" w:beforeAutospacing="1" w:after="0"/></w:pPr>""";

    private static readonly Length Fourteen = Length.FromPoints(14);

    /// <summary>The document's first paragraph takes no auto margin, and the one after it does.</summary>
    [Fact]
    public void TheDocumentsFirstParagraphTakesNoAutoMargin()
    {
        List<PageParagraph> paragraphs = MiniDocx
            .Layout($"""<w:p>{Auto}<w:r><w:t>First</w:t></w:r></w:p><w:p>{Auto}<w:r><w:t>Second</w:t></w:r></w:p>""")
            .Blocks.OfType<PageParagraph>().ToList();

        paragraphs[0].Format.SpaceBefore.ShouldBe(Length.Zero);
        paragraphs[1].Format.SpaceBefore.ShouldBe(Fourteen);
    }

    /// <summary>A stated <c>w:before</c> is not the auto margin and survives.</summary>
    [Fact]
    public void AStatedSpaceBeforeSurvives()
    {
        PageParagraph first = MiniDocx
            .Layout("""<w:p><w:pPr><w:spacing w:before="240"/></w:pPr><w:r><w:t>First</w:t></w:r></w:p>""")
            .Blocks.OfType<PageParagraph>().First();

        first.Format.SpaceBefore.ShouldBe(Length.FromPoints(12));
    }

    /// <summary>A text box's first paragraph takes no auto margin, and its second does.</summary>
    [Fact]
    public void AShapesFirstParagraphTakesNoAutoMargin()
    {
        string body = $"""
            <w:p><w:r><w:t>Anchor</w:t></w:r></w:p>
            <w:p><w:r>
              <w:drawing xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                         xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                         xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                <wp:anchor distT="0" distB="0" distL="0" distR="0" simplePos="0" relativeHeight="1"
                           behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1">
                  <wp:simplePos x="0" y="0"/>
                  <wp:positionH relativeFrom="column"><wp:posOffset>0</wp:posOffset></wp:positionH>
                  <wp:positionV relativeFrom="paragraph"><wp:posOffset>0</wp:posOffset></wp:positionV>
                  <wp:extent cx="1828800" cy="914400"/>
                  <wp:wrapNone/>
                  <wp:docPr id="1" name="Box"/>
                  <a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                    <wps:wsp>
                      <wps:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="1828800" cy="914400"/></a:xfrm>
                        <a:prstGeom prst="rect"><a:avLst/></a:prstGeom></wps:spPr>
                      <wps:txbx><w:txbxContent>
                        <w:p>{Auto}<w:r><w:t>Inside</w:t></w:r></w:p>
                        <w:p>{Auto}<w:r><w:t>Below</w:t></w:r></w:p>
                      </w:txbxContent></wps:txbx>
                      <wps:bodyPr/>
                    </wps:wsp>
                  </a:graphicData></a:graphic>
                </wp:anchor>
              </w:drawing>
            </w:r></w:p>
            """;

        PageFrame frame = MiniDocx.Layout(body).Blocks.OfType<PageParagraph>()
            .SelectMany(paragraph => paragraph.Frames).ShouldHaveSingleItem();

        List<PageParagraph> inside = frame.Blocks.OfType<PageParagraph>().ToList();
        inside[0].Format.SpaceBefore.ShouldBe(Length.Zero);
        inside[1].Format.SpaceBefore.ShouldBe(Fourteen);
    }
}
