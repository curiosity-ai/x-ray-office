using XRay.Office.TestKit.LibreOffice;
using Shouldly;

namespace XRay.Office.Fidelity.Tests;

/// <summary>
/// The configuration the reference <c>soffice</c> is run under: the machine's, less a local family
/// preference and <c>fonts-urw-base35</c>.
/// </summary>
/// <remarks>
/// Pure text in and text out, so it needs no LibreOffice and no fontconfig. What the configuration
/// buys is measured in <see cref="ReferenceFontconfig"/>'s own remarks; these pin the three rules
/// that build it, on the shapes this container's <c>/etc/fonts</c> actually has.
/// </remarks>
public sealed class ReferenceFontconfigTests
{
    private const string Root = """
        <?xml version="1.0"?>
        <fontconfig>
        	<dir>/usr/share/fonts</dir>
        	<include ignore_missing="yes">conf.d</include>
        	<cachedir>/var/cache/fontconfig</cachedir>
        </fontconfig>
        """;

    /// <summary>
    /// The drop-in directory becomes one include per kept file, in order, and the rejections are added.
    /// </summary>
    [Fact]
    public void TheDropInsAreIncludedOneByOneAndTheRejectionsAppended()
    {
        string text = ReferenceFontconfig.Compose(
            Root,
            ["/etc/fonts/conf.d/10-a.conf", "/etc/fonts/conf.d/60-b.conf"],
            ["/usr/share/fonts/opentype/urw-base35/NimbusSans-Regular.otf"])!;

        text.ShouldNotContain(">conf.d</include>");
        text.IndexOf("10-a.conf", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("60-b.conf", StringComparison.Ordinal));
        text.ShouldContain("<glob>/usr/share/fonts/opentype/urw-base35/NimbusSans-Regular.otf</glob>");
        text.IndexOf("<rejectfont>", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("</fontconfig>", StringComparison.Ordinal));

        // Everything the machine states besides the drop-in directory is kept as it is.
        text.ShouldContain("<cachedir>/var/cache/fontconfig</cachedir>");
    }

    /// <summary>A root that does not read its drop-ins the stock way is left alone.</summary>
    [Fact]
    public void AnUnfamiliarRootIsNotEdited()
        => ReferenceFontconfig.Compose("<fontconfig/>", [], []).ShouldBeNull();

    /// <summary>
    /// A local file that prefers a family is dropped; a packaged link, or a local rendering-only file,
    /// is kept.
    /// </summary>
    /// <remarks>
    /// The three shapes of this container's <c>conf.d</c>: <c>56-prefer-inter.conf</c> (a regular
    /// file of <c>&lt;alias&gt;&lt;prefer&gt;</c>), <c>12-unhinted-grayscale.conf</c> (a regular
    /// file of hinting edits) and everything else (links into <c>conf.avail</c>, some of which alias
    /// families too).
    /// </remarks>
    [Fact]
    public void OnlyALocalFamilyPreferenceIsDropped()
    {
        const string prefer = "<fontconfig><alias><family>sans-serif</family><prefer><family>Inter</family></prefer></alias></fontconfig>";
        const string hinting = "<fontconfig><match target=\"pattern\"><edit name=\"hinting\" mode=\"assign\"><bool>false</bool></edit></match></fontconfig>";

        ReferenceFontconfig.IsLocalFamilyPreference("56-prefer-inter.conf", isLink: false, prefer).ShouldBeTrue();
        ReferenceFontconfig.IsLocalFamilyPreference("60-latin.conf", isLink: true, prefer).ShouldBeFalse();
        ReferenceFontconfig.IsLocalFamilyPreference("12-unhinted-grayscale.conf", isLink: false, hinting).ShouldBeFalse();
    }

    /// <summary>
    /// A URW face is rejected in its own directory and under its copy's name elsewhere, and nothing
    /// else in a shared directory is.
    /// </summary>
    [Fact]
    public void AUrwFaceIsRejectedWhereverThePackagePutsIt()
    {
        HashSet<string> stems = new(StringComparer.Ordinal) { "NimbusSans-Regular", "C059-Roman" };

        ReferenceFontconfig.IsUrwBase35("/usr/share/fonts/opentype/urw-base35/NimbusSans-Regular.otf", stems)
            .ShouldBeTrue();
        ReferenceFontconfig.IsUrwBase35("/usr/share/fonts/X11/Type1/C059-Roman.pfb", stems).ShouldBeTrue();
        ReferenceFontconfig.IsUrwBase35("/usr/share/fonts/X11/Type1/c0648bt_.pfb", stems).ShouldBeFalse();
        ReferenceFontconfig.IsUrwBase35("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", stems).ShouldBeFalse();
    }
}
