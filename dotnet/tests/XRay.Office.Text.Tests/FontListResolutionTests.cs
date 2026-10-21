using XRay.Office.Text.Fonts;
using Shouldly;

namespace XRay.Office.Text.Tests;

/// <summary>
/// A <c>;</c>-separated family list resolves token by token, and only its last token reaches the
/// fontconfig model.
/// </summary>
/// <remarks>
/// tdf#108243 (ecd293dd31aa, in 26.8.1.1): <c>PhysicalFontCollection::FindFontFamily</c> asks the
/// pre-match hook only when <c>nTokenPos == -1</c>. Every expectation is 26.8.1.1's face for the
/// same list in <c>probes/calc-anchor-r26/fontlist.fodt</c>, read out of its PDF under the stock
/// fontconfig. 26.2.4.2 answered DejaVu Sans for the first, third and last of them.
/// </remarks>
public class FontListResolutionTests
{
    private static SystemFontResolver Resolver()
    {
        SystemFontIndex index = SystemFontIndex.Build();
        Assert.SkipWhen(index.FamilyCount == 0, "no fonts are installed; see check-env.sh");
        return new SystemFontResolver(index);
    }

    [Theory]
    [InlineData("NoSuchFontA;Liberation Mono", "Liberation Mono")]
    [InlineData("Liberation Serif;Liberation Mono", "Liberation Serif")]
    [InlineData("NoSuchFontA;Calibri", "Carlito")]
    [InlineData("Calibri;Liberation Mono", "Carlito")]
    [InlineData("Liberation Serif Greek;Liberation Mono", "Liberation Serif")]
    [InlineData("NoSuchFontA;DejaVu Sans Mono;Liberation Serif", "DejaVu Sans Mono")]
    public void TheFirstTokenThatResolvesWithoutFontconfigWins(string list, string expected)
    {
        SystemFontResolver resolver = Resolver();
        foreach (string family in new[] { "Liberation Mono", "Liberation Serif", "Carlito", "DejaVu Sans Mono" })
            Assert.SkipUnless(resolver.Index.Has(family), $"{family} is not installed");

        resolver.Resolve(new FontRequest(list)).FamilyName.ShouldBe(expected);
    }

    [Fact]
    public void WhenNoTokenIsInstalledTheLastOneIsSubstitutedLikeASingleName()
    {
        SystemFontResolver resolver = Resolver();

        // `NoSuchFontA;Courier` draws in what `Courier` alone resolves to — Liberation Mono under
        // the stock fontconfig — and not in what `NoSuchFontA` alone would.
        string alone = resolver.Resolve(new FontRequest("Courier")).FamilyName;
        resolver.Resolve(new FontRequest("NoSuchFontA;Courier")).FamilyName.ShouldBe(alone);
        resolver.Resolve(new FontRequest("NoSuchFontA;NoSuchFontB", DeclaredClass: FontFamilyClass.Serif))
            .FamilyName.ShouldBe(
                resolver.Resolve(new FontRequest("NoSuchFontB", DeclaredClass: FontFamilyClass.Serif))
                    .FamilyName);
    }

    [Theory]
    [InlineData("Times New Roman CYR", "Times New Roman")]
    [InlineData("Times New Roman CYR Greek", "Times New Roman")]
    [InlineData("Arial (Hebrew)", "Arial")]
    [InlineData("Arial Baltic", "Arial")]
    [InlineData("Arial", null)]
    [InlineData("Greek", null)]
    public void AScriptSuffixIsStrippedTheWayFontdefsStripsIt(string name, string? expected)
        => SystemFontResolver.StripScript(name).ShouldBe(expected);
}
