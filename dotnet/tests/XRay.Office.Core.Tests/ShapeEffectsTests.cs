using XRay.Office.Core.Geometry;
using XRay.Office.Core.Graphics;
using XRay.Office.Core.Units;
using Shouldly;

namespace XRay.Office.Core.Tests;

/// <summary>
/// The grid, the radii and the fallbacks of <see cref="ShapeEffects"/>, without a document.
/// </summary>
public sealed class ShapeEffectsTests
{
    private static readonly DocRect Page = new(Length.Zero, Length.Zero, Length.FromPoints(720), Length.FromPoints(540));

    [Fact]
    public void AGridUnderTheCapIsThePdfDevicesSevenHundredAndTwentyDpi()
    {
        // A 10 pt square with a 1 pt glow, twice: 352 + 70 hundredths of a millimetre is 119.6
        // device pixels at 720 dpi, so 120; the outer reaches 492, so 140. In 1/100 mm discretes
        // the grids would have been 422 and 492 pixels, and the reference's downsampled bitmaps
        // (50 and 59 at 300 dpi) say they were the device's.
        EffectContent square = new()
        {
            Fill = [GraphicsPath.Rectangle(new DocRect(
                Length.FromEmu(2000000), Length.FromEmu(800000), Length.FromEmu(127000), Length.FromEmu(127000)))],
            SolidColour = new Colour(0x44, 0x72, 0xC4),
        };

        IReadOnlyList<EffectLayer> layers = ShapeEffects.Glow(
            square, new Colour(255, 0, 0), Length.FromEmu(12700), twice: true, Page);

        layers.Count.ShouldBe(2);
        (layers[0].Image.Width, layers[0].Image.Height).ShouldBe((140, 140));
        (layers[1].Image.Width, layers[1].Image.Height).ShouldBe((120, 120));
    }

    [Fact]
    public void ContentThatNeedsPixelsIsNotSoftenedWithoutARasteriser()
    {
        EffectContent gradient = new()
        {
            Fill = [GraphicsPath.Rectangle(new DocRect(Length.Zero, Length.Zero, Length.FromPoints(100), Length.FromPoints(50)))],
            Draw = _ => { },
        };

        ShapeEffects.SoftEdge(gradient, Length.FromPoints(10), twice: false, Page).ShouldBeNull();
    }

    [Fact]
    public void ARecolouredEffectKeepsItsAlpha()
    {
        RasterImage glow = new() { Width = 2, Height = 1, Pixels = new byte[] { 255, 0, 0, 200, 255, 0, 0, 7 } };

        RasterImage shadow = ShapeEffects.Recoloured(glow, new Colour(10, 20, 30));

        shadow.Pixels.ToArray().ShouldBe(new byte[] { 10, 20, 30, 200, 10, 20, 30, 7 });
    }
}
