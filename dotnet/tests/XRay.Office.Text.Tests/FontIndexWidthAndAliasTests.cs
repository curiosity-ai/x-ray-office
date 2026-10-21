using XRay.Office.Text.Fonts;
using Shouldly;

namespace XRay.Office.Text.Tests;

/// <summary>
/// The installed-face index prefers a normal-width face and answers a legacy family name.
/// </summary>
/// <remarks>
/// Both measured on 26.8.1.1 (<c>probes/calc-anchor-r26/results.md</c>): with the DejaVu Sans
/// Condensed faces in a fontconfig directory, <c>DejaVu Sans</c> draws in DejaVu Sans and
/// <c>DejaVu Sans Condensed</c> in the condensed face — this tree drew the condensed face for
/// both, because the condensed file's typographic family is <c>DejaVu Sans</c> and it was enumerated
/// first — and <c>Inter Light</c>, Inter's legacy (name ID 1) family, draws in Inter Light where
/// this tree fell back to DejaVu Sans.
/// </remarks>
public class FontIndexWidthAndAliasTests
{
    private const string Regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    private static readonly string[] CondensedCandidates =
    [
        "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed.ttf",
        "/opt/libreoffice26.8/share/fonts/truetype/.condensed-aside/DejaVuSansCondensed.ttf",
        "/opt/libreoffice26.2/share/fonts/truetype/.condensed-aside/DejaVuSansCondensed.ttf",
    ];

    [Fact]
    public void ANormalWidthFaceWinsItsSlotWhicheverFileCameFirst()
    {
        string? condensed = CondensedCandidates.FirstOrDefault(File.Exists);
        Assert.SkipUnless(File.Exists(Regular) && condensed is not null, "DejaVu Sans and its Condensed face are not both here");

        // The condensed file in the first directory searched, so first-found alone would keep it.
        string root = Directory.CreateTempSubdirectory("xray-width-").FullName;
        try
        {
            string first = Directory.CreateDirectory(Path.Combine(root, "a")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(root, "b")).FullName;
            File.Copy(condensed!, Path.Combine(first, "DejaVuSansCondensed.ttf"));
            File.Copy(Regular, Path.Combine(second, "DejaVuSans.ttf"));

            SystemFontIndex index = SystemFontIndex.Build([first, second]);

            InstalledFace? plain = index.Best("DejaVu Sans", 400, italic: false);
            plain.ShouldNotBeNull();
            Path.GetFileName(plain.Value.Path).ShouldBe("DejaVuSans.ttf");
            plain.Value.WidthClass.ShouldBe(InstalledFace.NormalWidth);

            InstalledFace? narrow = index.Best("DejaVu Sans Condensed", 400, italic: false);
            narrow.ShouldNotBeNull("the condensed face is still reachable by its legacy family");
            Path.GetFileName(narrow.Value.Path).ShouldBe("DejaVuSansCondensed.ttf");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ALegacyFamilyNameFindsItsFaces()
    {
        const string Light = "/usr/share/fonts/opentype/inter/Inter-Light.otf";
        Assert.SkipUnless(File.Exists(Light), "Inter Light is not installed");

        SystemFontIndex index = SystemFontIndex.Build([Path.GetDirectoryName(Light)!]);

        index.Best("Inter Light", 400, italic: false)!.Value.Weight.ShouldBe(300);
        index.Best("Inter", 400, italic: false)!.Value.Weight.ShouldBe(400,
            "the typographic family is still the whole family");
    }
}
