using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Model;
using XRay.Office.WordProcessing.Ww8;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A DOC section's text grid (<c>sprmSClm</c>, <c>sprmSDyaLinePitch</c>, <c>sprmSDxtCharSpace</c>) and the
/// <c>Dop</c>'s automatic hyphenation, as <c>SwWW8ImplReader::SetDocumentGrid</c> and the stylesheet import
/// read them.
/// </summary>
/// <remarks>
/// Measured on 26.8's daily with its own <c>.doc</c> export of <c>probes/words-chargrid/</c> and
/// <c>probes/words-textgrid-r268/</c>, and with patched copies of them
/// (<c>probes/words-chargrid/patch-sep.py</c>): every line of eight grid variants breaks where the reference
/// breaks it, and the three line-grid variants agree to 0.01 pt where they were up to 60 pt apart.
/// </remarks>
public sealed class DocTextGridTests
{
    [Theory]
    [InlineData(0, null, false)]
    [InlineData(2, null, false)]
    [InlineData(1, 230L, false)]
    [InlineData(3, 230L, true)]
    [InlineData(7, 230L, true)]
    public void TheGridTypeDecidesWhatSnaps(int clm, long? width, bool snaps)
    {
        TextGridSnap? grid = Grid(clm, 312, 4096);

        if (clm == 0)
        {
            grid.ShouldBeNull();
            return;
        }

        grid.ShouldNotBeNull().Pitch.ShouldBe(Length.FromTwips(312));

        if (width is null)
        {
            grid.Value.Characters.ShouldBeNull();
            return;
        }

        CharacterGrid characters = grid.Value.Characters.ShouldNotBeNull();
        characters.Width.ShouldBe(Length.FromTwips(width.Value));
        characters.BaseFontHeight.ShouldBe(Length.FromTwips(210));
        characters.SnapsToCharacters.ShouldBe(snaps);
    }

    /// <summary>
    /// The character space is added in twips with no trip through hundredths of a millimetre:
    /// −3426 is −1 pt and three twentieths, so 210 − 20 + 3.
    /// </summary>
    [Theory]
    [InlineData(-3426, 193L)]
    [InlineData(2048, 220L)]
    [InlineData(40960, 410L)]
    // Unlike DOCX, a character grid stating no character space is still one, at the document's own size.
    [InlineData(0, 210L)]
    public void TheCharacterSpaceIsAddedInTwips(int charSpace, long width)
        => Grid(1, 312, charSpace).ShouldNotBeNull().Characters.ShouldNotBeNull().Width
            .ShouldBe(Length.FromTwips(width));

    /// <summary>A pitch outside 1 to 31680 twips leaves the item's own 400; no minimum of 100 applies.</summary>
    [Theory]
    [InlineData(50, 50L)]
    [InlineData(0, 400L)]
    [InlineData(31681, 400L)]
    public void TheLinePitchIsTakenAsStated(int pitch, long expected)
        => Grid(2, pitch, 0).ShouldNotBeNull().Pitch.ShouldBe(Length.FromTwips(expected));

    /// <summary>With no default style size the width starts from 240 twips.</summary>
    [Fact]
    public void WithoutADocumentFontTheWidthStartsAtTwelvePoints()
        => Ww8SectionTable.ReadProperties(Sprms(1, 312, 4096)).TextGrid.ShouldNotBeNull()
            .Characters.ShouldNotBeNull().Width.ShouldBe(Length.FromTwips(260));

    /// <summary><c>fAutoHyphen</c> is bit 0x10 of the byte at 0x05; nothing else about hyphenation is read.</summary>
    [Fact]
    public void TheDopsAutomaticHyphenationIsOneBit()
    {
        byte[] dop = new byte[0x204];
        Ww8DocumentProperties.Parse(dop).AutoHyphenation.ShouldBeFalse();

        dop[5] = 0x10;
        Ww8DocumentProperties.Parse(dop).AutoHyphenation.ShouldBeTrue();
    }

    /// <summary><c>fDontAdjustLineHeightInTable</c> is bit 0x08 of the second compatibility word.</summary>
    [Fact]
    public void TheTableGridFlagIsReadFromTheSecondCompatibilityWord()
    {
        byte[] dop = new byte[0x204];
        Ww8DocumentProperties.Parse(dop).KeepsTableLinesOffTheGrid.ShouldBeFalse();

        dop[0x200] = 0x08;
        Ww8DocumentProperties.Parse(dop).KeepsTableLinesOffTheGrid.ShouldBeTrue();
    }

    /// <summary>
    /// A stated line spacing of nought is at least nought — the text's own height on a grid — not single.
    /// </summary>
    [Fact]
    public void AStatedNoughtIsAtLeastNought()
    {
        new Ww8LayoutFormat { LineSpacing = 0 }.ToParagraphFormat(Length.FromPoints(11)).LineSpacing
            .ShouldBe(LineSpacingRule.AtLeast(Length.Zero));
        new Ww8LayoutFormat().ToParagraphFormat(Length.FromPoints(11)).LineSpacing
            .ShouldBe(LineSpacingRule.SingleSpaced);
    }

    private static TextGridSnap? Grid(int clm, int pitch, int charSpace)
        => Ww8SectionTable.ReadProperties(Sprms(clm, pitch, charSpace), Length.FromTwips(210)).TextGrid;

    private static byte[] Sprms(int clm, int pitch, int charSpace)
        =>
        [
            0x32, 0x50, (byte)clm, (byte)(clm >> 8),
            0x31, 0x90, (byte)pitch, (byte)(pitch >> 8),
            0x30, 0x70, (byte)charSpace, (byte)(charSpace >> 8), (byte)(charSpace >> 16), (byte)(charSpace >> 24),
        ];
}
