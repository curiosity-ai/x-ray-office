using System.Buffers.Binary;
using XRay.Office.Core.Units;
using XRay.Office.WordProcessing.Layout;
using XRay.Office.WordProcessing.Ww8;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A default tab interval the file states as nothing, or as almost nothing, in the two Word formats —
/// whose importers answer it differently, both measured against 26.8.1.1.
/// </summary>
public sealed class DefaultTabIntervalTests
{
    /// <summary>
    /// DOCX: <c>w:defaultTabStop w:val="0"</c> is one hundredth of a millimetre (tdf#168607), not half
    /// an inch.
    /// </summary>
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("100", 100L)]
    public void ADocxStatingZeroTakesOneHundredthOfAMillimetre(string stated, long twips)
    {
        PageParagraph paragraph = MiniDocx
            .Layout("<w:p><w:r><w:t>A</w:t></w:r></w:p>", settings: $"""<w:defaultTabStop w:val="{stated}"/>""")
            .Blocks.OfType<PageParagraph>().First();

        paragraph.Format.DefaultTabInterval.ShouldBe(twips == 0 ? Length.FromMm100(1) : Length.FromTwips(twips));
    }

    /// <summary>DOC: a <c>dxaTab</c> below 56 twips is Writer's 709 (<c>ww8par.cxx</c>:1975-1977).</summary>
    [Theory]
    [InlineData(0, 709)]
    [InlineData(30, 709)]
    [InlineData(-5, 709)]
    [InlineData(56, 56)]
    [InlineData(100, 100)]
    public void ADocBelowFiftySixTwipsTakesWritersDefault(short stated, long twips)
    {
        byte[] dop = new byte[16];
        BinaryPrimitives.WriteInt16LittleEndian(dop.AsSpan(10), stated);

        Ww8DocumentProperties.Parse(dop).DefaultTabInterval.ShouldBe(Length.FromTwips(twips));
    }
}
