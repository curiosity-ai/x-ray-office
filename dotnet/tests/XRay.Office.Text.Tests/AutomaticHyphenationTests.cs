using XRay.Office.Core.Globalization;
using XRay.Office.Core.Units;
using XRay.Office.Text.Fonts;
using XRay.Office.Text.Layout;
using Shouldly;

namespace XRay.Office.Text.Tests;

/// <summary>
/// The line filler hyphenates the word that runs past the margin when the paragraph asks for it, by
/// Writer's rule: only that word, at the last pattern point that still fits with its hyphen, and not at
/// all when a blank lies inside the hyphenation zone.
/// </summary>
/// <remarks>
/// <para>
/// Laid out in Liberation Mono so that a width can be stated in characters and every case says what the
/// rule is rather than restating one face's metrics: the hyphen is one character wide like every other.
/// The rule itself is <c>SwTextGuess::Guess</c> (<c>sw/source/core/text/guess.cxx</c>:302-420) and the
/// hyphenator's choice of point (<c>lingucomponent/source/hyphenator/hyphen/hyphenimp.cxx</c>:437-448).
/// </para>
/// <para>
/// Most cases use an invented language whose "dictionary" allows a break between any two characters, so
/// that which point is chosen is decided by the paragraph's own limits alone. One case uses the shipped
/// <c>hyph_en_US.dic</c> and asserts the break 26.8.1.1 draws.
/// </para>
/// </remarks>
public sealed class AutomaticHyphenationTests
{
    private const string Anywhere = "x-hyphenate-anywhere";

    private static readonly Length Size = Length.FromPoints(10);

    static AutomaticHyphenationTests()
        => Hyphenators.Register(Anywhere, new EveryPoint());

    [Fact]
    public void WithoutHyphenationTheWordIsCarriedWhole()
    {
        List<TextLine> lines = Fill("aaa bbbbbbbbbb", 8, null, Anywhere);

        lines[0].VisibleEnd.ShouldBe(3);
        lines[0].Hyphenated.ShouldBeFalse();
    }

    [Fact]
    public void TheOverflowingWordIsBrokenWhereItStillFitsWithItsHyphen()
    {
        // Eight and a half characters of room, so that the fit is not decided by rounding: "aaa bbbb"
        // fits, and with a hyphen after it only "aaa bbb" does.
        List<TextLine> lines = Fill("aaa bbbbbbbbbb", 8.5, HyphenationRules.Default, Anywhere);

        lines[0].Hyphenated.ShouldBeTrue();
        lines[0].End.ShouldBe(7, "aaa bbb and a hyphen are eight characters");
        lines[0].VisibleEnd.ShouldBe(7);
        lines[0].Width.Points.ShouldBe(Room(8).Points, 0.01, "the line's width includes the hyphen");
        lines[1].Start.ShouldBe(7);
        lines[1].Hyphenated.ShouldBeFalse();
    }

    [Fact]
    public void TheTrailingMinimumMovesTheBreakLeft()
    {
        // Ten letters and nine of room: eight fit with the hyphen, but three must move to the next line.
        HyphenationRules rules = HyphenationRules.Default with { MinTrailing = 3 };

        List<TextLine> lines = Fill("bbbbbbbbbb", 9, rules, Anywhere);

        lines[0].Hyphenated.ShouldBeTrue();
        lines[0].End.ShouldBe(7);
    }

    [Fact]
    public void AWordShorterThanTheMinimumLengthIsNotHyphenated()
    {
        List<TextLine> lines = Fill("aaa bbbb", 6, HyphenationRules.Default, Anywhere);

        lines[0].Hyphenated.ShouldBeFalse();
        lines[0].VisibleEnd.ShouldBe(3);
    }

    [Fact]
    public void ABlankInsideTheZoneEndsTheLineThere()
    {
        // The blank after "aaa" is four characters from the margin; a zone of five takes it in.
        HyphenationRules rules = HyphenationRules.Default with { Zone = Room(5) };

        List<TextLine> lines = Fill("aaa bbbbbbbbbb", 8, rules, Anywhere);

        lines[0].Hyphenated.ShouldBeFalse();
        lines[0].VisibleEnd.ShouldBe(3);
    }

    [Fact]
    public void ABlankOutsideTheZoneDoesNotStopIt()
    {
        HyphenationRules rules = HyphenationRules.Default with { Zone = Room(2) };

        Fill("aaa bbbbbbbbbb", 8, rules, Anywhere)[0].Hyphenated.ShouldBeTrue();
    }

    [Fact]
    public void ALineTheLayoutForbadeIsNotHyphenated()
    {
        // The paginator's hyphenation-keep: the line starting at 0 ends a page.
        HyphenationRules rules = HyphenationRules.Default with { NoHyphenationAt = new HashSet<int> { 0 } };

        List<TextLine> lines = Fill("aaa bbbbbbbbbb cccccccccc", 8, rules, Anywhere);

        lines[0].Hyphenated.ShouldBeFalse();
        lines[1].Hyphenated.ShouldBeTrue("only the line named is affected");
    }

    [Fact]
    public void AWordInCapitalsIsLeftWholeWhenTheParagraphSaysSo()
    {
        HyphenationRules rules = HyphenationRules.Default with { NoCaps = true };

        Fill("aaa BBBBBBBBBB", 8, rules, Anywhere)[0].Hyphenated.ShouldBeFalse();
        Fill("aaa Bbbbbbbbbb", 8, rules, Anywhere)[0].Hyphenated.ShouldBeTrue();
    }

    [Fact]
    public void TheConsecutiveLimitLeavesTheNextLineWhole()
    {
        HyphenationRules rules = HyphenationRules.Default with { MaxConsecutive = 1 };

        List<TextLine> lines = Fill("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 8, rules, Anywhere);

        lines[0].Hyphenated.ShouldBeTrue();
        lines[1].Hyphenated.ShouldBeFalse("one hyphenated line in a row is the limit");
        lines[2].Hyphenated.ShouldBeTrue("and the count starts again after an unhyphenated one");
    }

    [Fact]
    public void ALanguageWithNoDictionaryIsNotHyphenated()
    {
        // LibreOffice's answer too: no matching dictionary, and `hyphenate` returns nothing.
        Fill("aaa bbbbbbbbbb", 8, HyphenationRules.Default, "x-no-such-dictionary")[0]
            .Hyphenated.ShouldBeFalse();
    }

    [Fact]
    public void EnglishBreaksAtTheLastPatternPointThatFits()
    {
        Assert.SkipWhen(Hyphenators.For("en-US") is null, "hyph_en_US.dic is not beside the assembly");

        // 26.8.1.1 ends the first line of the RTF probe "Characterisation of internationalisa-", and the
        // shipped patterns allow nothing between "internationalisa" and the room that is left.
        const string text = "Characterisation of internationalisation requirements";

        List<TextLine> lines = Fill(text, 38, HyphenationRules.Default, "en-US");

        lines[0].Hyphenated.ShouldBeTrue();
        text[..lines[0].End].ShouldBe("Characterisation of internationalisa");
    }

    private static List<TextLine> Fill(string text, double characters, HyphenationRules? rules, string language)
        => new LineFiller(Measurer()).Fill(
            text,
            Size,
            Room(characters),
            language: language,
            tabs: new ParagraphFormat { Hyphenation = rules });

    private static Length Room(double characters)
    {
        Length one = Measurer().Measure("m", Size);
        return Length.FromEmu((long)Math.Round(one.Emu * characters));
    }

    private static TextMeasurer Measurer()
    {
        string? path = FindFont("LiberationMono-Regular.ttf");
        Assert.SkipWhen(path is null, "Liberation Mono is not installed; see check-env.sh");
        return new TextMeasurer(OpenTypeFace.ReadFile(path!).ShouldNotBeNull());
    }

    private static string? FindFont(string fileName)
    {
        foreach (string directory in new[] { "/usr/share/fonts/truetype", "/usr/share/fonts" })
        {
            if (!Directory.Exists(directory)) continue;

            string? found = Directory
                .EnumerateFiles(directory, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (found is not null) return found;
        }

        return null;
    }

    /// <summary>A hyphenator that allows a break between any two characters.</summary>
    private sealed class EveryPoint : IHyphenator
    {
        public IReadOnlyList<int> FindHyphenationPoints(ReadOnlySpan<char> word, string language)
            => Enumerable.Range(1, Math.Max(0, word.Length - 1)).ToArray();
    }
}
