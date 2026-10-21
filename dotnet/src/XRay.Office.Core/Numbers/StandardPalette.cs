namespace XRay.Office.Core.Numbers;

/// <summary>
/// LibreOffice's standard colour palette, which a format code's <c>[COLOR n]</c> indexes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a renderer carries a UI palette.</strong> <c>[COLOR n]</c> is resolved through
/// <c>SvNumberFormatter::GetUserDefColor(n − 1)</c>, which Calc wires to the document's
/// <c>XColorList</c> (<c>sc/source/core/data/documen9.cxx</c>:246-259) — the list
/// <c>XColorList::CreateStdColorList()</c> loads from <c>share/palette/standard.soc</c>. Since
/// tdf#146404 (d74f55ef4f06, in 26.8.1.1) the ODF importer goes the other way as well: a data
/// style's <c>fo:color</c> that is none of the ten keyword colours is looked up in the same list
/// and written as <c>[COLORn]</c> (<c>xmloff/source/style/xmlnumfi.cxx</c>, <c>AddColor</c>), so
/// a palette colour is painted where it used to be dropped. The file is byte-identical in 26.2.4.2
/// and 26.8.1.1, so pinning it couples this to nothing that has moved.
/// </para>
/// <para>
/// The values are the file's 120 <c>draw:color</c> entries in document order; no value appears
/// twice, so the first-occurrence rule of <c>XColorList::GetIndexOfColor</c>
/// (<c>svx/source/xoutdev/xtabcolr.cxx</c>:159-170) and a plain search agree.
/// </para>
/// </remarks>
public static class StandardPalette
{
    /// <summary>
    /// The highest <c>n</c> a format code's <c>[COLOR n]</c> may name: <c>ImpSvNumberformatScan::GetColor</c>
    /// accepts <c>1 ≤ n ≤ 64</c> (<c>svl/source/numbers/zforscan.cxx</c>:631-637) and answers
    /// nothing for any other index, which makes the whole code invalid.
    /// </summary>
    public const int MaxFormatIndex = 64;

    private static readonly uint[] Rgb =
    [
        0x000000, 0x111111, 0x1C1C1C, 0x333333, 0x666666, 0x808080, 0x999999, 0xB2B2B2,
        0xCCCCCC, 0xDDDDDD, 0xEEEEEE, 0xFFFFFF, 0xFFFF00, 0xFFBF00, 0xFF8000, 0xFF4000,
        0xFF0000, 0xBF0041, 0x800080, 0x55308D, 0x2A6099, 0x158466, 0x00A933, 0x81D41A,
        0xFFFFD7, 0xFFF5CE, 0xFFDBB6, 0xFFD8CE, 0xFFD7D7, 0xF7D1D5, 0xE0C2CD, 0xDEDCE6,
        0xDEE6EF, 0xDEE7E5, 0xDDE8CB, 0xF6F9D4, 0xFFFFA6, 0xFFE994, 0xFFB66C, 0xFFAA95,
        0xFFA6A6, 0xEC9BA4, 0xBF819E, 0xB7B3CA, 0xB4C7DC, 0xB3CAC7, 0xAFD095, 0xE8F2A1,
        0xFFFF6D, 0xFFDE59, 0xFF972F, 0xFF7B59, 0xFF6D6D, 0xE16173, 0xA1467E, 0x8E86AE,
        0x729FCF, 0x81ACA6, 0x77BC65, 0xD4EA6B, 0xFFFF38, 0xFFD428, 0xFF860D, 0xFF5429,
        0xFF3838, 0xD62E4E, 0x8D1D75, 0x6B5E9B, 0x5983B0, 0x50938A, 0x3FAF46, 0xBBE33D,
        0xE6E905, 0xE8A202, 0xEA7500, 0xED4C05, 0xF10D0C, 0xA7074B, 0x780373, 0x5B277D,
        0x3465A4, 0x168253, 0x069A2E, 0x5EB91E, 0xACB20C, 0xB47804, 0xB85C00, 0xBE480A,
        0xC9211E, 0x861141, 0x650953, 0x55215B, 0x355269, 0x1E6A39, 0x127622, 0x468A1A,
        0x706E0C, 0x784B04, 0x7B3D00, 0x813709, 0x8D281E, 0x611729, 0x4E102D, 0x481D32,
        0x383D3C, 0x28471F, 0x224B12, 0x395511, 0x443205, 0x472702, 0x492300, 0x4B2204,
        0x50200C, 0x41190D, 0x3B160E, 0x3A1A0F, 0x362413, 0x302709, 0x2E2706, 0x342A06,
    ];

    /// <summary>The number of entries, 120.</summary>
    public static int Count => Rgb.Length;

    /// <summary>The RGB value of the zero-based entry <paramref name="index"/>, or null outside the list.</summary>
    /// <param name="index">The zero-based entry.</param>
    public static uint? At(int index) => index >= 0 && index < Rgb.Length ? Rgb[index] : null;

    /// <summary>The zero-based index of an RGB value, or −1 when the palette does not hold it.</summary>
    /// <param name="rgb">The colour, as <c>0xRRGGBB</c>.</param>
    public static int IndexOf(uint rgb) => Array.IndexOf(Rgb, rgb & 0xFFFFFF);
}
