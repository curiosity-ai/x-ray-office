using XRay.Office.OpenDocument.Styles;
using Shouldly;

namespace XRay.Office.OpenDocument.Tests;

/// <summary>
/// An ODF family list is read as <c>XMLFontFamilyNamePropHdl::importXML</c> reads it: split at
/// unquoted commas, trimmed, one pair of matching quotes stripped, joined with <c>;</c>.
/// </summary>
public class OdfFontFamiliesTests
{
    [Theory]
    [InlineData("'NoSuchFontA', 'Liberation Mono'", "NoSuchFontA;Liberation Mono")]
    [InlineData("Calibri, 'Liberation Mono'", "Calibri;Liberation Mono")]
    [InlineData("'Wingdings 2'", "Wingdings 2")]
    [InlineData("Liberation Sans", "Liberation Sans")]
    [InlineData("\"Gill Sans, Bold\", Arial", "Gill Sans, Bold;Arial")]
    [InlineData("  A ,, B  ", "A;B")]
    [InlineData("'unbalanced", "'unbalanced")]
    [InlineData("", null)]
    [InlineData(" , ", null)]
    public void AListIsSplitAtUnquotedCommasAndJoinedWithSemicolons(string value, string? expected)
        => OdfFontFamilies.Parse(value).ShouldBe(expected);

    [Fact]
    public void TheFirstFamilyIsTheOneBeforeTheFirstSeparator()
    {
        OdfFontFamilies.First("NoSuchFontA;Liberation Mono").ShouldBe("NoSuchFontA");
        OdfFontFamilies.First("Carlito").ShouldBe("Carlito");
    }
}
