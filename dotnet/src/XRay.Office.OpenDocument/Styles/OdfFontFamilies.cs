using System.Text;

namespace XRay.Office.OpenDocument.Styles;

/// <summary>
/// Reads an ODF font-family list — <c>svg:font-family</c> or <c>fo:font-family</c> — the way
/// LibreOffice's importer does.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is a CSS-style list, <c>'Gill Sans', Calibri, sans-serif</c>.
/// <c>XMLFontFamilyNamePropHdl::importXML</c> (<c>xmloff/source/style/fonthdl.cxx</c>:67-115)
/// splits it at the commas that are not inside quotes, trims each entry, strips one pair of
/// matching quotes and joins what is left with <c>;</c>, which is the separator VCL's font
/// lookup walks token by token (<c>PhysicalFontCollection::FindFontFamily</c>). Every reader
/// here used to keep the first entry alone.
/// </para>
/// <para>
/// <strong>Why the whole list matters since 26.8.1.1.</strong> tdf#108243 (ecd293dd31aa) asks the
/// fontconfig pre-match hook only for the <em>last</em> token. Before it, the first missing token
/// went straight to fontconfig, which always answers, so a later token was never reached and
/// keeping the first was what LibreOffice did. Now a missing first family gives way to an
/// installed second one: measured on 26.8.1.1, <c>'NoSuchFontA', 'Liberation Mono'</c> draws in
/// Liberation Mono where 26.2.4.2 draws DejaVu Sans. The resolver walks the list; see
/// <c>SystemFontResolver.Resolve</c>.
/// </para>
/// </remarks>
public static class OdfFontFamilies
{
    /// <summary>The separator LibreOffice joins a family list with.</summary>
    public const char Separator = ';';

    /// <summary>
    /// The families a list names, unquoted and joined with <see cref="Separator"/>, or null when it
    /// names none.
    /// </summary>
    /// <param name="list">The attribute's value.</param>
    public static string? Parse(string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) return null;

        StringBuilder joined = new();
        int position = 0;

        while (true)
        {
            int comma = IndexOfComma(list, position);
            int first = position;
            int last = (comma < 0 ? list.Length : comma) - 1;

            while (last > first && list[last] == ' ') last--;
            while (first <= last && list[first] == ' ') first++;

            char quote = first > last ? '\0' : list[first];
            if (first < last && quote is '\'' or '"' && list[last] == quote)
            {
                first++;
                last--;
            }

            if (first <= last)
            {
                if (joined.Length > 0) joined.Append(Separator);
                joined.Append(list, first, last - first + 1);
            }

            if (comma < 0) break;
            position = comma + 1;
        }

        return joined.Length == 0 ? null : joined.ToString();
    }

    /// <summary>The first family of a list, for a question only one family can answer.</summary>
    /// <param name="families">A list as <see cref="Parse"/> returns it.</param>
    public static string? First(string? families)
    {
        if (families is null) return null;

        int separator = families.IndexOf(Separator, StringComparison.Ordinal);
        return separator < 0 ? families : families[..separator];
    }

    /// <summary>
    /// The next comma outside quotes, as <c>sax::Converter::indexOfComma</c> finds it.
    /// </summary>
    private static int IndexOfComma(string text, int from)
    {
        char quote = '\0';

        for (int i = from; i < text.Length; i++)
        {
            char c = text[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == ',')
            {
                return i;
            }
        }

        return -1;
    }
}
