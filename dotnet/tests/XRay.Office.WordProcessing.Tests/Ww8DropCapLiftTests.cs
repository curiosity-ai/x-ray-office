using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Ww8;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// <see cref="Ww8DocumentReader.LiftTextFrames"/>'s handling of a drop-cap text frame, in isolation.
/// </summary>
public sealed class Ww8DropCapLiftTests
{
    private static readonly Ww8TextFramePosition Framed = Ww8TextFramePosition.None with { Width = 1440 };

    /// <summary>A lone drop-cap paragraph becomes the next paragraph's drop rather than a frame.</summary>
    [Fact]
    public void ADropCapFrameDropsIntoTheNextParagraph()
    {
        List<Ww8LayoutBlock> lifted = Ww8DocumentReader.LiftTextFrames(
            [Block("W", Framed, new Ww8DropCap(3, 120)), Block("AFTER text")]);

        lifted.Count.ShouldBe(1);
        Ww8DocumentReader.Ww8LayoutParagraph host = lifted[0].Paragraph!.Value;
        host.Text.ShouldBe("AFTER text");
        host.TextFrames.ShouldBeNull();
        host.DropLetters.ShouldNotBeNull();
        host.DropLetters!.Letters.Text.ShouldBe("W");
        host.DropLetters.Lines.ShouldBe(3);
        host.DropLetters.Distance.ShouldBe(120);
    }

    /// <summary>The same frame without a DCS is an ordinary text frame, anchored on the next paragraph.</summary>
    [Fact]
    public void AFrameWithoutADropCapStaysAFrame()
    {
        List<Ww8LayoutBlock> lifted = Ww8DocumentReader.LiftTextFrames(
            [Block("W", Framed), Block("AFTER text")]);

        lifted.Count.ShouldBe(1);
        lifted[0].Paragraph!.Value.DropLetters.ShouldBeNull();
        lifted[0].Paragraph!.Value.TextFrames!.Count.ShouldBe(1);
    }

    private static Ww8LayoutBlock Block(
        string text, Ww8TextFramePosition frame = default, Ww8DropCap? drop = null)
        => new(new Ww8DocumentReader.Ww8LayoutParagraph(
            0, text, ParagraphFormat.Default, "Liberation Serif", Length.FromPoints(12), 400, false, null, false)
        {
            TextFrame = frame,
            DropCap = drop,
        });
}
