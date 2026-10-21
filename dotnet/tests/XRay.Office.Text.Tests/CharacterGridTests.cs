using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using Shouldly;

namespace XRay.Office.Text.Tests;

/// <summary>
/// The character half of Writer's text grid: which characters it reaches, the cell each takes, and where a
/// glyph sits in its cell.
/// </summary>
/// <remarks>
/// <c>sw::Justify::SnapToGrid</c> and <c>lcl_MsoCompatSnapToGridEdge</c>
/// (<c>sw/source/core/txtnode/justify.cxx</c>:42-71, 176-253). The numbers are the ones 26.8's daily draws on
/// <c>probes/words-chargrid/</c>: a 10.5 pt ideograph on a 230-twip <c>linesAndChars</c> grid over a
/// 210-twip document font is 11.5 pt wide, on a 193-twip one 9.65, and on a 193-twip <c>snapToChars</c>
/// grid it takes two cells, 19.3 pt, drawn 4.4 pt in.
/// </remarks>
public class CharacterGridTests
{
    private static readonly CharacterGrid Spaced = new(Length.FromTwips(230), Length.FromTwips(210), false);
    private static readonly CharacterGrid Squeezed = new(Length.FromTwips(193), Length.FromTwips(210), false);
    private static readonly CharacterGrid Snapping = new(Length.FromTwips(193), Length.FromTwips(210), true);

    [Fact]
    public void LinesAndCharsAddsTheGridLessTheDocumentFontToEveryCluster()
    {
        Spaced.Cell(Length.FromTwips(210)).ShouldBe(Length.FromTwips(230));
        Squeezed.Cell(Length.FromTwips(210)).ShouldBe(Length.FromTwips(193));

        // Whatever the run's own size: a 16 pt ideograph is 320 twips and still gains 20.
        Spaced.Cell(Length.FromTwips(320)).ShouldBe(Length.FromTwips(340));
        Spaced.Offset(Length.FromTwips(210), '永').ShouldBe(Length.Zero);
    }

    [Fact]
    public void SnapToCharsTakesWholeCellsAndCentresTheGlyph()
    {
        Snapping.Cell(Length.FromTwips(210)).ShouldBe(Length.FromTwips(386));
        Snapping.Offset(Length.FromTwips(210), '永').ShouldBe(Length.FromTwips(88));

        // One cell at least, so a narrow blank is widened to the pitch.
        Snapping.Cell(Length.FromTwips(52)).ShouldBe(Length.FromTwips(193));

        // An exact multiple takes no extra cell: `(w - 1) / pitch + 1`.
        Snapping.Cell(Length.FromTwips(386)).ShouldBe(Length.FromTwips(386));
    }

    [Fact]
    public void BracketsAndStopsLeanTowardsTheTextTheyBelongTo()
    {
        // An opening bracket against the following edge, a closing one and a full stop against the preceding.
        Snapping.Offset(Length.FromTwips(210), '「').ShouldBe(Length.FromTwips(176));
        Snapping.Offset(Length.FromTwips(210), '」').ShouldBe(Length.Zero);
        Snapping.Offset(Length.FromTwips(210), '。').ShouldBe(Length.Zero);
        Snapping.Offset(Length.FromTwips(210), '，').ShouldBe(Length.FromTwips(88));
    }

    [Fact]
    public void AClusterWidthIsTruncatedToWholeTwips()
    {
        // 210.9 twips is a `KernArray` difference of 210.9 assigned to a `tools::Long`.
        Length advance = Length.FromEmu(210 * Length.EmuPerTwip + 571);
        Snapping.Cell(advance).ShouldBe(Length.FromTwips(386));
        Snapping.Offset(advance, '永').ShouldBe(Length.FromTwips(88));
    }

    [Fact]
    public void AWeakCharacterTakesTheScriptBeforeIt()
    {
        // "P4 mixed 中文 text": the blank after `d` is Latin, the blank after `文` is East Asian.
        bool[] asian = CharacterGrid.AsianCharacters("d 中文 t");
        asian.ShouldBe([false, false, true, true, true, false]);
    }

    [Fact]
    public void WeakTextOpeningAParagraphTakesTheFirstStrongScript()
    {
        // A blank is weak; an ASCII digit is not, `getCompatibilityScriptClassByBlock` filing Basic Latin
        // as Latin whole.
        CharacterGrid.AsianCharacters("\u00A0 汉").ShouldBe([true, true, true]);
        CharacterGrid.AsianCharacters("  a").ShouldBe([false, false, false]);
        CharacterGrid.AsianCharacters("1汉").ShouldBe([false, true]);
    }

    [Fact]
    public void TheMeasurementChargesEachEastAsianClusterItsCell()
    {
        // The prefix table, not a re-shaping: a Latin character is untouched and an ideograph takes its cell.
        const string Path = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc";
        if (!File.Exists(Path)) Assert.Skip("WenQuanYi Zen Hei is not installed.");
        Fonts.OpenTypeFace face = Fonts.OpenTypeFace.ReadFile(Path).ShouldNotBeNull();

        const string Text = "a汉字";
        FormattedRun run = new(0, Text.Length, face, Length.FromPoints(10.5));

        MeasuredParagraph plain = MeasuredParagraph.Measure(Text, [run]);
        MeasuredParagraph gridded = MeasuredParagraph.Measure(Text, [run], characterGrid: Spaced);

        (gridded.WidthBetween(0, 1) - plain.WidthBetween(0, 1)).ShouldBe(Length.Zero);
        ((double)(gridded.WidthBetween(0, 3) - plain.WidthBetween(0, 3)).Twips).ShouldBe(40, 1);
    }
}
