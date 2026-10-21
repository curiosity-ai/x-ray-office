using System.Buffers.Binary;

namespace XRay.Office.Rendering.Pdf;

/// <summary>
/// Just enough of a <c>CFF </c> table to answer the one question a PDF writer has to ask of it:
/// is it CID-keyed?
/// </summary>
/// <remarks>
/// <para>
/// A CFF font comes in two shapes and PDF admits them in different places. A <em>name-keyed</em>
/// CFF addresses its glyphs through a charset of names and is what a simple 8-bit font
/// dictionary references — <c>/Subtype /Type1</c> with the program in a <c>/FontFile3</c>. A
/// <em>CID-keyed</em> one replaces the charset with a CID mapping and its top dictionary carries
/// a registry-ordering-supplement triple; PDF 1.7 §9.7.4.2 admits it only as a
/// <c>CIDFontType0</c> descendant of a composite <c>/Type0</c> font, and a reader handed one
/// under a simple dictionary has nothing to do but reject it. Poppler says so out loud:
/// *"Missing or empty DescendantFonts entry in Type 0 font"*, once per page, and then draws no
/// glyphs at all.
/// </para>
/// <para>
/// Both shapes are common. Measured on this machine's eleven installed <c>.otf</c> faces: the
/// four Loma faces are name-keyed and all seven Unifont faces are CID-keyed — and Unifont is
/// the last-resort fallback, so it is the one a document reaches by accident.
/// </para>
/// <para>
/// Nothing else about CFF is parsed. The top dictionary is scanned for one operator and the
/// operand encoding is skipped over, which is the whole of what distinguishing the two takes.
/// </para>
/// </remarks>
internal static class CompactFontFormat
{
    /// <summary>The two-byte operator <c>12 30</c>, <c>ROS</c>, which only a CID-keyed font has.</summary>
    private const byte RegistryOrderingSupplement = 30;

    /// <summary>The escape byte introducing a two-byte operator.</summary>
    private const byte Escape = 12;

    /// <summary>
    /// Whether an sfnt's <c>CFF </c> table is CID-keyed, and so cannot go in a simple font.
    /// </summary>
    /// <param name="font">A whole font file, which need not be CFF-flavoured.</param>
    /// <returns>False for anything this cannot read, which is the safe answer: it means "embed".</returns>
    public static bool IsCidKeyed(ReadOnlySpan<byte> font)
    {
        if (SfntTables.Read(font)?.Table("CFF ") is not { Length: > 4 } table) return false;

        // The header states its own length, so a version this does not know still parses.
        int at = table[2];
        if (at < 4 || at >= table.Length) return false;

        if (!SkipIndex(table, ref at)) return false;              // Name INDEX
        if (!ReadFirstIndexEntry(table, ref at, out ReadOnlySpan<byte> top)) return false;

        return HasRegistryOrdering(top);
    }

    /// <summary>Steps over an INDEX without reading its entries.</summary>
    private static bool SkipIndex(ReadOnlySpan<byte> data, ref int at)
        => ReadFirstIndexEntry(data, ref at, out _);

    /// <summary>
    /// Reads an INDEX's first entry and leaves <paramref name="at"/> past the whole structure.
    /// </summary>
    /// <remarks>
    /// An INDEX is a count, an offset size, <c>count + 1</c> offsets of that size, and then the
    /// data — whose first byte is at <c>offset[0]</c> counted from <em>one before</em> the data
    /// block, which is the format's one genuine oddity and the reason offsets start at 1.
    /// </remarks>
    private static bool ReadFirstIndexEntry(
        ReadOnlySpan<byte> data, ref int at, out ReadOnlySpan<byte> first)
    {
        first = default;
        if (at + 2 > data.Length) return false;

        int count = BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
        if (count == 0) { at += 2; return true; }

        if (at + 3 > data.Length) return false;
        int size = data[at + 2];
        if (size is < 1 or > 4) return false;

        int offsets = at + 3;
        long span = (long)(count + 1) * size;
        if (offsets + span > data.Length) return false;

        int block = offsets + (int)span - 1;
        int start = Offset(data, offsets, size, 0);
        int end = Offset(data, offsets, size, count);
        if (start < 1 || end < start || block + end > data.Length) return false;

        int firstEnd = Offset(data, offsets, size, 1);
        if (firstEnd >= start && block + firstEnd <= data.Length)
        {
            first = data.Slice(block + start, firstEnd - start);
        }

        at = block + end;
        return true;
    }

    /// <summary>One big-endian offset of an INDEX's offset array.</summary>
    private static int Offset(ReadOnlySpan<byte> data, int offsets, int size, int index)
    {
        int value = 0;
        for (int i = 0; i < size; i++) value = (value << 8) | data[offsets + (index * size) + i];
        return value;
    }

    /// <summary>Whether a top dictionary carries the <c>ROS</c> operator.</summary>
    /// <remarks>
    /// A DICT is operands then an operator, repeatedly. Operands are self-describing by their
    /// first byte, so the scan can step over every one of them without knowing what any operator
    /// means — which is what keeps this from being a CFF parser.
    /// </remarks>
    private static bool HasRegistryOrdering(ReadOnlySpan<byte> dictionary)
    {
        int at = 0;

        while (at < dictionary.Length)
        {
            byte b = dictionary[at];

            switch (b)
            {
                case Escape:
                    if (at + 1 < dictionary.Length && dictionary[at + 1] == RegistryOrderingSupplement)
                    {
                        return true;
                    }

                    at += 2;
                    break;

                case <= 21:                              // a one-byte operator
                    at += 1;
                    break;

                case 28:                                 // a 16-bit integer operand
                    at += 3;
                    break;

                case 29:                                 // a 32-bit integer operand
                    at += 5;
                    break;

                case 30:                                 // a real, nibble-encoded, 0xF terminated
                    at += 1;
                    while (at < dictionary.Length)
                    {
                        byte pair = dictionary[at++];
                        if ((pair & 0x0F) == 0x0F || (pair >> 4) == 0x0F) break;
                    }

                    break;

                case >= 32 and <= 246:                   // a small integer, one byte
                    at += 1;
                    break;

                case >= 247 and <= 254:                  // a medium integer, two bytes
                    at += 2;
                    break;

                default:                                 // 22-27 and 31 are reserved
                    at += 1;
                    break;
            }
        }

        return false;
    }
    /// <summary>The one-byte operator <c>15</c>, <c>charset</c>: the offset of the charset.</summary>
    private const byte CharsetOperator = 15;

    /// <summary>
    /// The CID each glyph of a CID-keyed <c>CFF </c> table stands for, indexed by glyph id, or null
    /// when the table cannot be read or is not CID-keyed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CIDFontType0 descendant selects a glyph of a CID-keyed program by CID through the program's
    /// own charset (PDF 1.7 §9.7.4.2), and <c>hb-subset</c> keeps the original CIDs in a subset's
    /// charset while it renumbers the glyphs. So a PDF code that names a glyph has to be mapped to
    /// that glyph's CID, which is what master's <c>vcl/source/fontsubset/CFFCharset.cxx</c> reads
    /// for the same purpose (be1a60ef339c). A name-keyed program needs none of this: there the CID
    /// is the glyph id.
    /// </para>
    /// <para>
    /// The charset is read in its three formats (CFF spec, Technical Note #5176 §13): format 0 one
    /// two-byte CID per glyph, formats 1 and 2 ranges with a one- and two-byte count. Glyph 0 is
    /// <c>.notdef</c>, CID 0, and is not stored.
    /// </para>
    /// </remarks>
    /// <param name="cff">The <c>CFF </c> table.</param>
    /// <param name="glyphs">How many glyphs the program holds.</param>
    public static ushort[]? CidsByGlyph(ReadOnlySpan<byte> cff, int glyphs)
    {
        if (cff.Length <= 4 || glyphs <= 0) return null;

        int at = cff[2];
        if (at < 4 || at >= cff.Length) return null;

        if (!SkipIndex(cff, ref at)) return null;                      // Name INDEX
        if (!ReadFirstIndexEntry(cff, ref at, out ReadOnlySpan<byte> top)) return null;
        if (!HasRegistryOrdering(top)) return null;
        if (Operand(top, CharsetOperator) is not { } offset || offset <= 0 || offset >= cff.Length)
            return null;

        ushort[] cids = new ushort[glyphs];
        int glyph = 1;
        int position = offset;
        byte format = cff[position++];

        while (glyph < glyphs)
        {
            switch (format)
            {
                case 0:
                    if (position + 2 > cff.Length) return null;
                    cids[glyph++] = BinaryPrimitives.ReadUInt16BigEndian(cff[position..]);
                    position += 2;
                    break;

                case 1 or 2:
                    int width = format == 1 ? 1 : 2;
                    if (position + 2 + width > cff.Length) return null;

                    int first = BinaryPrimitives.ReadUInt16BigEndian(cff[position..]);
                    int left = format == 1
                        ? cff[position + 2]
                        : BinaryPrimitives.ReadUInt16BigEndian(cff[(position + 2)..]);
                    position += 2 + width;

                    for (int n = 0; n <= left && glyph < glyphs; n++)
                        cids[glyph++] = (ushort)(first + n);
                    break;

                default:
                    return null;
            }
        }

        return cids;
    }

    /// <summary>The integer operand a one-byte operator takes in a DICT, or null.</summary>
    /// <remarks>The operand last pushed before the operator, which is the only one it takes.</remarks>
    private static int? Operand(ReadOnlySpan<byte> dictionary, byte op)
    {
        int at = 0;
        int? last = null;

        while (at < dictionary.Length)
        {
            byte b = dictionary[at];

            switch (b)
            {
                case Escape:
                    at += 2;
                    last = null;
                    break;

                case <= 21:
                    if (b == op) return last;
                    at += 1;
                    last = null;
                    break;

                case 28:
                    if (at + 3 > dictionary.Length) return null;
                    last = BinaryPrimitives.ReadInt16BigEndian(dictionary[(at + 1)..]);
                    at += 3;
                    break;

                case 29:
                    if (at + 5 > dictionary.Length) return null;
                    last = BinaryPrimitives.ReadInt32BigEndian(dictionary[(at + 1)..]);
                    at += 5;
                    break;

                case 30:
                    at += 1;
                    while (at < dictionary.Length)
                    {
                        byte pair = dictionary[at++];
                        if ((pair & 0x0F) == 0x0F || (pair >> 4) == 0x0F) break;
                    }

                    last = null;
                    break;

                case >= 32 and <= 246:
                    last = b - 139;
                    at += 1;
                    break;

                case >= 247 and <= 250:
                    if (at + 2 > dictionary.Length) return null;
                    last = ((b - 247) * 256) + dictionary[at + 1] + 108;
                    at += 2;
                    break;

                case >= 251 and <= 254:
                    if (at + 2 > dictionary.Length) return null;
                    last = (-(b - 251) * 256) - dictionary[at + 1] - 108;
                    at += 2;
                    break;

                default:
                    at += 1;
                    break;
            }
        }

        return null;
    }
}
