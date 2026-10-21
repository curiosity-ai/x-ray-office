using System.Xml.Linq;
using XRay.Office.Core;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Graphics;
using XRay.Office.Presentations.Layout;
using XRay.Office.Presentations.Ooxml;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.Presentations.Tests;

/// <summary>
/// <c>a:uFill</c>: the colour a run's underline is drawn in, as 26.8.1.1 and 26.2.4.2 draw it.
/// </summary>
/// <remarks>
/// <c>underline-colour.pptx</c> holds seven red underlined runs, one per case, and both references
/// stroke their underlines <c>FF0000</c>, <c>00B050</c>, <c>000000</c>, <c>FF0000</c>,
/// <c>0000FF</c>, <c>00B050</c> and <c>0000FF</c>: no fill, an RGB fill, a theme fill, an
/// <c>a:uFillTx</c>, an <c>a:uLn</c> alone, an RGB fill under <c>lumMod 50%</c>, and a preset.
/// The theme case's black is the reference's bug, followed on purpose; the <c>a:uLn</c> case is
/// the reference turning the run's text blue as well, which is not modelled, so this tree draws
/// that one red. See <c>PptxTextBody.UnderlineColour</c>.
/// </remarks>
public class SlideUnderlineColourTests
{
    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    [Fact]
    public void EachUnderlineIsDrawnInTheColourTheReferenceDraws()
    {
        using IDocument read = new PresentationReader().Read(
            DocumentSource.FromFile(Corpus.Require("underline-colour.pptx")));
        IReadOnlyList<LaidOutSlide> slides = ((SlidePages)((IPaginatedDocument)read).Layout()).Slides;

        List<Colour> underlines =
        [
            .. slides[0].Shapes
                .Where(shape => shape.Text is { Runs.Count: > 0 })
                .SelectMany(shape => shape.Text!.Runs)
                .Where(run => run.Rules is { Count: > 0 })
                .Select(run => run.UnderlineColour ?? run.Colour),
        ];

        underlines.ShouldBe(
        [
            Colour.FromRgb(0xFF0000),
            Colour.FromRgb(0x00B050),
            Colour.FromRgb(0x000000),
            Colour.FromRgb(0xFF0000),
            Colour.FromRgb(0xFF0000),
            Colour.FromRgb(0x00B050),
            Colour.FromRgb(0x0000FF),
        ]);
    }

    [Fact]
    public void AnInheritedUnderlineFillReachesTheRunAndUFillTxCancelsIt()
    {
        XElement level = new(
            XName.Get("lvl1pPr", DrawingNamespace),
            new XElement(
                XName.Get("defRPr", DrawingNamespace),
                Fill("srgbClr", "00B050")));

        PptxTextBody.Read(Body(), inherited: _ => [level])
            .Paragraphs[0].Runs[0].UnderlineColour.ShouldBe(Colour.FromRgb(0x00B050));

        PptxTextBody.Read(
                Body(new XElement(XName.Get("uFillTx", DrawingNamespace))),
                inherited: _ => [level])
            .Paragraphs[0].Runs[0].UnderlineColour.ShouldBeNull();
    }

    [Fact]
    public void AColourModelTheImporterDoesNotConvertLeavesTheTextColour()
    {
        PptxTextBody.Read(Body(Fill("sysClr", null)))
            .Paragraphs[0].Runs[0].UnderlineColour.ShouldBeNull();
    }

    private static XElement Fill(string model, string? value)
    {
        XElement colour = new(XName.Get(model, DrawingNamespace));
        if (value is not null) colour.SetAttributeValue("val", value);
        else colour.SetAttributeValue("val", "windowText");

        return new XElement(
            XName.Get("uFill", DrawingNamespace),
            new XElement(XName.Get("solidFill", DrawingNamespace), colour));
    }

    private static XElement Body(params XElement[] runProperties)
        => new(
            XName.Get("txBody", DrawingNamespace),
            new XElement(XName.Get("bodyPr", DrawingNamespace)),
            new XElement(
                XName.Get("p", DrawingNamespace),
                new XElement(
                    XName.Get("r", DrawingNamespace),
                    new XElement(
                        XName.Get("rPr", DrawingNamespace),
                        new XAttribute("u", "sng"),
                        runProperties),
                    new XElement(XName.Get("t", DrawingNamespace), "Text"))));
}
