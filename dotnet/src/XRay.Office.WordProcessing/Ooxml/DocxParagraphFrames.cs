using System.Xml.Linq;
using XRay.Office.Core.Units;
using XRay.Office.Ooxml;
using XRay.Office.WordProcessing.Layout;

namespace XRay.Office.WordProcessing.Ooxml;

/// <summary>
/// Reads a DOCX paragraph's <c>w:framePr</c> the way writerfilter does.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Only a paragraph that states its own <c>w:framePr</c> is framed.</strong> Frame mode is set
/// when the element is met inside a paragraph's properties (<c>DomainMapper.cxx</c>:2831-2843 on the
/// 26.8 branch); met in a style it only records the attributes, under a <c>TODO</c>. So a style's
/// <c>w:framePr</c> frames nothing by itself and fills in the attributes of a paragraph that does
/// frame itself — both measured on the 26.8 daily, a style-only frame laid out in the flow and the same
/// style under a paragraph stating only <c>w:w</c> placed where the style says. The document defaults
/// are not a layer at all: one corpus document states a <c>w:framePr</c> in <c>w:pPrDefault</c> and
/// frames nothing.
/// </para>
/// <para>
/// <c>w:yAlign="inline"</c> on the paragraph switches frame mode back off (bnc#780851,
/// <c>DomainMapper.cxx</c>:1104-1122): the paragraph stays in the flow.
/// </para>
/// </remarks>
public static class DocxParagraphFrames
{
    /// <summary>What a paragraph's <c>w:framePr</c> asks of it, or null when it is not framed.</summary>
    /// <param name="paragraph">The <c>w:p</c>.</param>
    /// <param name="styles">The document's styles, for the attributes the paragraph leaves to its style.</param>
    public static ParagraphFrameRequest? RequestOf(XElement? paragraph, WordStyles styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        XElement? properties = Word.Child(paragraph, "pPr");
        if (Word.Child(properties, "framePr") is not { } direct) return null;

        ParagraphFrameLayer own = Layer(direct, out bool inline);
        if (inline) return null;

        string? styleId = Word.Value(properties, "pStyle") ?? styles.DefaultStyleId(WordStyleType.Paragraph);
        XElement? defaults = Word.Child(styles.DefaultParagraphProperties, "framePr");

        List<ParagraphFrameLayer> chain = [own];
        foreach (XElement layer in styles.ParagraphPropertyLayers("framePr", null, styleId))
        {
            if (ReferenceEquals(layer, defaults)) continue;
            chain.Add(Layer(layer, out _));
        }

        ParagraphFramePosition position = ParagraphFrameLayer.Resolve(chain);

        if (own.DropCap == 0) return new ParagraphFrameRequest(position);

        // `finishParagraph` reads the drop cap's lines and distance from the paragraph's own properties
        // only, and clamps both (`DomainMapper_Impl.cxx`:2528-2534): an absent or out-of-range line
        // count is two, and a distance outside a 16-bit twip count is none.
        int lines = own.Lines is > 0 and < sbyte.MaxValue ? own.Lines : 2;
        long distance = own.HorizontalSpace is > 0 and < short.MaxValue ? own.HorizontalSpace.Value : 0;
        return new ParagraphFrameRequest(position, lines, Length.FromTwips(distance));
    }

    /// <summary>
    /// One <c>w:framePr</c>'s attributes, applied in document order as <c>lcl_attribute</c> applies them.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="inline">True when it states <c>w:yAlign="inline"</c>.</param>
    internal static ParagraphFrameLayer Layer(XElement element, out bool inline)
    {
        ParagraphFrameLayer layer = new();
        inline = false;

        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.Name.Namespace != OoxmlNamespaces.WordprocessingML) continue;

            string value = attribute.Value;
            long? number = long.TryParse(value, out long parsed) ? parsed : null;

            switch (attribute.Name.LocalName)
            {
                case "dropCap": layer.DropCap = value switch { "drop" => 1, "margin" => 2, _ => 0 }; break;
                case "lines": layer.Lines = (int)Math.Clamp(number ?? 0, int.MinValue, int.MaxValue); break;
                case "hAnchor": layer.HorizontalAnchor = Relation(value) ?? layer.HorizontalAnchor; break;
                case "vAnchor": layer.VerticalAnchor = Relation(value) ?? layer.VerticalAnchor; break;
                case "x":
                    if (number is null) break;
                    layer.X = number;
                    layer.XAlign = ParagraphFrameHorizontalAlignment.None;
                    break;
                case "xAlign":
                    layer.XAlign = value switch
                    {
                        "center" => ParagraphFrameHorizontalAlignment.Centre,
                        "right" => ParagraphFrameHorizontalAlignment.Right,
                        "inside" => ParagraphFrameHorizontalAlignment.Inside,
                        "outside" => ParagraphFrameHorizontalAlignment.Outside,
                        "left" => ParagraphFrameHorizontalAlignment.Left,
                        _ => ParagraphFrameHorizontalAlignment.None,
                    };
                    break;
                case "y":
                    if (number is null) break;
                    layer.Y = number;
                    layer.YAlign = ParagraphFrameVerticalAlignment.None;
                    break;
                case "yAlign":
                    if (value == "inline") inline = true;
                    layer.YAlign = value switch
                    {
                        "top" or "inside" => ParagraphFrameVerticalAlignment.Top,
                        "center" => ParagraphFrameVerticalAlignment.Centre,
                        "bottom" or "outside" => ParagraphFrameVerticalAlignment.Bottom,
                        _ => ParagraphFrameVerticalAlignment.None,
                    };
                    break;
                case "hRule":
                    layer.HeightRule = value switch
                    {
                        "exact" => ParagraphFrameHeightRule.Exact,
                        "atLeast" => ParagraphFrameHeightRule.AtLeast,
                        _ => ParagraphFrameHeightRule.Auto,
                    };
                    break;

                // `through` and `auto` are DYNAMIC, `around` PARALLEL, `none` THROUGH, and everything else —
                // `notBeside` and `tight` included — NONE (`DomainMapper.cxx`:1146-1165). Measured on the
                // daily: `tight` leaves no text beside the frame, exactly as `notBeside` does.
                case "wrap":
                    layer.Wrap = value switch
                    {
                        "through" or "auto" => TextWrap.Optimal,
                        "around" => TextWrap.Both,
                        "none" => TextWrap.Through,
                        _ => TextWrap.TopAndBottom,
                    };
                    break;
                case "w": layer.Width = number ?? layer.Width; break;
                case "h": layer.Height = number ?? layer.Height; break;
                case "hSpace": layer.HorizontalSpace = number ?? layer.HorizontalSpace; break;
                case "vSpace": layer.VerticalSpace = number ?? layer.VerticalSpace; break;
            }
        }

        return layer;
    }

    private static ParagraphFrameRelation? Relation(string value) => value switch
    {
        "text" => ParagraphFrameRelation.Text,
        "margin" => ParagraphFrameRelation.Margin,
        "page" => ParagraphFrameRelation.Page,
        _ => null,
    };
}
