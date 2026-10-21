using System.Text;
using XRay.Office.Core.Documents;
using XRay.Office.Core.Units;
using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// RTF's <c>\hyphpar</c> stated on a style, and stated without a document <c>\hyphauto</c>.
/// </summary>
/// <remarks>
/// It is a <c>suppressAutoHyphens</c>, which DomainMapper maps to <c>ParaIsHyphenation = !value</c>, and RTF's
/// style import writes no default over it. Every expectation is 26.8's daily on <c>probes/rtf-hyphpar/</c>,
/// seven one-attribute RTF documents, each line-for-line identical to this tree's rendering.
/// </remarks>
public sealed class RtfStyleHyphenationTests
{
    [Theory]
    [InlineData(@"\hyphauto1", "", "", true)]
    [InlineData(@"\hyphauto1", @"\hyphpar0", @"\s1", false)]
    [InlineData(@"\hyphauto1", @"\hyphpar0", @"\s2", false)]
    [InlineData(@"\hyphauto1", @"\hyphpar0", @"\s1\hyphpar", true)]
    [InlineData("", "", @"\hyphpar", true)]
    [InlineData("", @"\hyphpar", @"\s1", true)]
    [InlineData("", "", "", false)]
    public void AStylesHyphparReachesItsParagraphs(string settings, string style, string paragraph, bool hyphenates)
        => (Format(settings, style, paragraph).Hyphenation is not null).ShouldBe(hyphenates);

    /// <summary>
    /// The zone and the keep come with <c>\hyphauto</c>, <c>\hyphcaps0</c> or <c>\widowctrl</c>; a document
    /// stating none of them hyphenates by the pool's rules.
    /// </summary>
    [Fact]
    public void TheZoneComesWithTheDocumentSettings()
    {
        Format("", "", @"\hyphpar").Hyphenation.ShouldBe(HyphenationRules.Default);
        Format(@"\widowctrl", "", @"\hyphpar").Hyphenation.ShouldNotBeNull().Zone.ShouldBe(Length.FromTwips(360));
        Format(@"\hyphauto1", "", "").Hyphenation.ShouldNotBeNull().KeepsLastLine.ShouldBeTrue();
    }

    private static ParagraphFormat Format(string settings, string style, string paragraph)
    {
        string rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0\froman Liberation Serif;}}"
                     + @"{\stylesheet{\s0\f0\fs22 Normal;}{\s1\sbasedon0" + style + @"\f0\fs22 Body;}"
                     + @"{\s2\sbasedon1\f0\fs22 Child;}}"
                     + settings + @"\pard\plain" + paragraph + @"\f0\fs22 Words.\par}";

        using DocumentSource source = DocumentSource.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(rtf)), "h.rtf");
        using IDocument document = new WordProcessingReader().Read(source);
        WordProcessingPages pages = (WordProcessingPages)((IPaginatedDocument)document).Layout();

        return pages.Blocks.OfType<PageParagraph>().First().Format;
    }
}
