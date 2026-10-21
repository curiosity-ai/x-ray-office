using System.Text;
using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// The paragraph Writer puts after a table that ends an RTF header, and the formatting the 26.8 branch
/// gives it: the default paragraph style's and nothing stated.
/// </summary>
/// <remarks>
/// <c>features/header-table.rtf</c>'s header is one row of 9 pt Carlito over a 0.31 in minimum height and
/// closes <c>\cell\row\pard }</c>. The 26.8 daily lays the trailing paragraph out at Standard's 12 pt
/// Liberation Serif whatever the group states, which outgrows the minimum and puts the body's first baseline
/// at 749.89 — 2.45 pt below 26.2.4.2's 752.34, where that paragraph kept the cell's 9 pt and fitted.
/// </remarks>
public sealed class RtfFurnitureTrailingParagraphTests
{
    private const string Ending = @"Chapter seven}\cell\row\pard }";

    /// <summary>The stated size of an untyped trailing paragraph does not reach it.</summary>
    [Theory]
    [InlineData(@"Chapter seven}\cell\row\pard\fs36 }")]
    [InlineData(@"Chapter seven}\cell\row\pard\plain\fs36 }")]
    [InlineData(@"Chapter seven}\cell\row}")]
    public void AnUntypedTrailingParagraphIsTheDefaultStyle(string ending)
        => BodyTop(ending).Points.ShouldBe(BodyTop(Ending).Points, 0.01);

    /// <summary>It is taller than the 9 pt row and pushes the body down by what overflows the minimum.</summary>
    [Fact]
    public void ItOutgrowsTheHeadersMinimumHeight()
        => (BodyTop(Ending) - BodyTop(@"Chapter seven}\cell\row\pard\par}")).Points.ShouldBe(2.45, 0.01);

    /// <summary>A <c>\par</c> after the row ends a paragraph that keeps its stated size: 18 pt here.</summary>
    [Fact]
    public void AnEndedParagraphKeepsItsOwnSize()
        => (BodyTop(@"Chapter seven}\cell\row\pard\fs36\par}") - BodyTop(@"Chapter seven}\cell\row\pard\par}"))
            .Points.ShouldBe(10.65, 0.01);

    private static Core.Units.Length BodyTop(string ending)
    {
        string rtf = File.ReadAllText(Corpus.Require("features/header-table.rtf"));
        rtf.ShouldContain(Ending);

        List<DrawnWord> words = ParagraphFrameTests.Draw(
            Encoding.Latin1.GetBytes(rtf.Replace(Ending, ending)), "header-table.rtf");
        return Core.Units.Length.FromPoints(words.First(word => word.Text == "Paragraph").Baseline);
    }
}
