using System.Xml.Linq;
using XRay.Office.WordProcessing.OpenDocument;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// tdf#168777 in ODF: a line's trailing blank carries its underline and strikethrough unless the document
/// asks for Word's trailing blanks without Word's "underline trailing space".
/// </summary>
/// <remarks>
/// <c>IsPaintTrailingSpaceDecorations</c> (<c>sw/source/core/text/portxt.cxx</c>:251-258) is
/// <c>!MsWordCompTrailingBlanks || MsWordUlTrailSpace</c>. Measured on 26.8's daily with one zipped
/// <c>.odt</c> patched to each combination (<c>probes/odt-trailblank/</c>; a flat ODF's settings are not
/// read): the 3 pt rule over every wrapped line's trailing blank is drawn in three combinations and absent
/// in the fourth, and this tree now draws the same rules on all four.
/// </remarks>
public sealed class OdtTrailingBlankTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("false", "false", true)]
    [InlineData("true", "false", false)]
    [InlineData("true", null, false)]
    [InlineData("true", "true", true)]
    [InlineData("false", "true", true)]
    public void OnlyWordsTrailingBlanksWithoutItsUnderlineLeaveTheBlankBare(
        string? compatibleBlanks, string? underlinesBlanks, bool decorates)
        => OdtLayoutSource.DecoratesTrailingBlank(
                Settings(("MsWordCompTrailingBlanks", compatibleBlanks), ("MsWordUlTrailSpace", underlinesBlanks)))
            .ShouldBe(decorates);

    private static XElement Settings(params (string Name, string? Value)[] items)
    {
        XNamespace config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";
        XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

        XElement set = new(config + "config-item-set", new XAttribute(config + "name", "ooo:configuration-settings"));
        foreach ((string name, string? value) in items)
        {
            if (value is null) continue;
            set.Add(new XElement(
                config + "config-item",
                new XAttribute(config + "name", name),
                new XAttribute(config + "type", "boolean"),
                value));
        }

        return new XElement(office + "settings", set);
    }
}
