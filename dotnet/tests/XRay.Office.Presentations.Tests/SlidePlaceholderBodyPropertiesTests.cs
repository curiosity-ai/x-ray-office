using System.Xml.Linq;
using XRay.Office.Core;
using XRay.Office.Core.Documents;
using XRay.Office.Presentations.Layout;
using XRay.Office.Presentations.Ooxml;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// What a slide placeholder's own <c>a:bodyPr</c> does to the autofit and the wrap it would
/// otherwise inherit, as LibreOffice 26.2.4.2 and 26.8.1.1 both read it.
/// </summary>
/// <remarks>
/// <para>
/// A slide placeholder starts from a copy of its layout's text body and every <c>a:bodyPr</c> it
/// reads then writes <c>TextFitToSize = NONE</c>, <c>TextAutoGrowHeight = false</c> and
/// <c>TextWordWrap = (wrap == square)</c> before its children are seen
/// (<c>oox/source/drawingml/textbodypropertiescontext.cxx:64-65, 205-207</c> at tag 26.8.1.1). So
/// a slide's <c>&lt;a:bodyPr/&gt;</c> discards the layout's <c>a:normAutofit</c> and its
/// <c>wrap="none"</c>; only a <c>p:txBody</c> with no <c>a:bodyPr</c> at all keeps them. That is
/// not PowerPoint's rule, and master after 26.8 (<c>52277daba816</c>, <c>763cf92d195c</c>) moves
/// towards PowerPoint's; the benchmark is 26.8.1.1.
/// </para>
/// <para>
/// And a title, body or object placeholder with rectangular geometry is a presentation object, a
/// text frame, which wraps at its width whatever <c>wrap</c> says — word wrap is a custom-shape
/// property in <c>svx</c>. A body placeholder given a <c>roundRect</c> is imported as a custom
/// shape and honours <c>wrap="none"</c>.
/// </para>
/// <para>
/// <c>placeholder-autofit.pptx</c>: a layout body stating <c>a:normAutofit</c>, overflowing 20 pt
/// text, and on the slides <c>&lt;a:bodyPr/&gt;</c>, <c>&lt;a:bodyPr&gt;&lt;a:normAutofit/&gt;</c>
/// and no <c>a:bodyPr</c>. Average glyph-box height in both references: 22.33, 15.63, 15.63 pt.
/// <c>placeholder-wrap.pptx</c>: a layout body stating <c>wrap="none"</c> with
/// <c>a:noAutofit</c>, and on the slides <c>&lt;a:bodyPr/&gt;</c>, <c>wrap="none"</c>,
/// <c>wrap="square"</c>, and <c>wrap="none"</c> on a <c>roundRect</c> body. Lines drawn in both
/// references: 9, 9, 9, 3.
/// </para>
/// </remarks>
public class SlidePlaceholderBodyPropertiesTests
{
    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private static IReadOnlyList<LaidOutSlide> Slides(string deck)
    {
        using IDocument read =
            new PresentationReader().Read(DocumentSource.FromFile(Corpus.Require(deck)));

        return ((SlidePages)((IPaginatedDocument)read).Layout()).Slides;
    }

    private static PlacedText BodyText(LaidOutSlide slide)
        => slide.Shapes.Single(shape => shape.Text is { Runs.Count: > 0 }).Text!;

    [Fact]
    public void ASlidesOwnBodyPrDiscardsTheLayoutsNormAutofit()
    {
        IReadOnlyList<LaidOutSlide> slides = Slides("placeholder-autofit.pptx");
        slides.Count.ShouldBe(3);

        double Size(int slide) => BodyText(slides[slide]).Runs.Max(run => run.Run.FontSize.Points);

        Size(0).ShouldBe(20.0, 0.05, "<a:bodyPr/> on the slide: no autofit, the authored 20 pt");
        Size(1).ShouldBeLessThan(19.0, "the slide states a:normAutofit itself");
        Size(2).ShouldBeLessThan(19.0, "no a:bodyPr at all keeps the layout's a:normAutofit");
    }

    [Fact]
    public void APresentationObjectWrapsWhateverItsWrapSays()
    {
        IReadOnlyList<LaidOutSlide> slides = Slides("placeholder-wrap.pptx");
        slides.Count.ShouldBe(4);

        int Lines(int slide)
            => BodyText(slides[slide]).Runs
                .Select(run => Math.Round(run.Run.Origin.Y.Points, 1))
                .Distinct()
                .Count();

        Lines(0).ShouldBe(9, "<a:bodyPr/> wraps, whatever the layout's wrap");
        Lines(1).ShouldBe(9, "a rectangular body placeholder ignores its own wrap=\"none\"");
        Lines(2).ShouldBe(9, "wrap=\"square\"");
        Lines(3).ShouldBe(3, "a roundRect body is a custom shape and keeps wrap=\"none\"");
    }

    [Fact]
    public void AnOwnBodyPrStatingNoAutofitDiscardsAnInheritedOne()
    {
        PptxTextBody.Read(Body(Properties()), inheritedBodyProperties: [Properties("normAutofit")])
            .AutoFit.ShouldBeFalse();
    }

    [Fact]
    public void ABodyWithNoBodyPrKeepsTheInheritedAutofit()
    {
        PptxTextBody.Read(Body(null), inheritedBodyProperties: [Properties("normAutofit")])
            .AutoFit.ShouldBeTrue();
    }

    [Fact]
    public void AnOwnBodyPrWrapsEvenWhenTheLayoutSaysNone()
    {
        PptxTextBody.Read(
                Body(Properties()),
                inheritedBodyProperties: [Properties("noAutofit", wrap: "none")])
            .Wraps.ShouldBeTrue();
    }

    [Fact]
    public void AnInheritedSpAutoFitDoesNotOverrideAnOwnWrapNone()
    {
        PptxTextBody.Read(
                Body(Properties(wrap: "none")),
                inheritedBodyProperties: [Properties("spAutoFit")])
            .Wraps.ShouldBeFalse();
    }

    [Fact]
    public void APresentationObjectIgnoresItsOwnWrapNone()
    {
        PptxTextBody.Read(Body(Properties(wrap: "none")), presentationObject: true)
            .Wraps.ShouldBeTrue();
    }

    private static XElement Body(XElement? properties)
    {
        XElement body = new(XName.Get("txBody", DrawingNamespace));
        if (properties is not null) body.Add(properties);
        body.Add(
            new XElement(
                XName.Get("p", DrawingNamespace),
                new XElement(
                    XName.Get("r", DrawingNamespace),
                    new XElement(XName.Get("t", DrawingNamespace), "Text"))));
        return body;
    }

    private static XElement Properties(string? autofit = null, string? wrap = null)
    {
        XElement properties = new(XName.Get("bodyPr", DrawingNamespace));
        if (wrap is not null) properties.SetAttributeValue("wrap", wrap);
        if (autofit is not null) properties.Add(new XElement(XName.Get(autofit, DrawingNamespace)));
        return properties;
    }
}
