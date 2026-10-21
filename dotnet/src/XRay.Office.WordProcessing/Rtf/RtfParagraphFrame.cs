using XRay.Office.Text.Layout;
using XRay.Office.WordProcessing.Layout;

namespace XRay.Office.WordProcessing.Rtf;

/// <summary>What an RTF paragraph's frame words say, as writerfilter's <c>RTFFrame</c> holds it.</summary>
/// <remarks>
/// <para>
/// RTF's absolutely positioned paragraphs — <c>\absw</c>, <c>\posx</c>, <c>\phpg</c> and their kin — are
/// Word's paragraph frames, and writerfilter does not lay them out itself: <c>RTFFrame::getSprms</c>
/// (<c>sw/source/writerfilter/rtftok/rtfdocumentimpl.cxx</c>:4142-4235 on the 26.8 branch) restates them
/// as a <c>w:framePr</c> on the paragraph and hands that to the same <c>DomainMapper</c> a DOCX goes
/// through. So this only translates; <see cref="ParagraphFrameLayer.Resolve"/> and
/// <see cref="ParagraphFrames"/> are shared with DOCX.
/// </para>
/// <para>
/// Three of the translation's choices decide where a frame goes and differ from what DOCX writes for
/// itself. The anchors are always stated, an unstated horizontal one as the column and an unstated
/// vertical one as the <em>margin</em> (:4196-4207), so a frame with no <c>\pvpara</c> is positioned
/// from the top of the body rather than from its paragraph. Both alignments are always stated as
/// well, an unset one as <c>NONE</c>. And <c>\posy</c> is one twip further down than it says
/// (<c>rtfdispatchvalue.cxx</c>:359-366, <em>"offset is also added on docx-export in Word"</em>). All
/// three measured on the 26.8 daily: <c>\posy2880</c> with no <c>\pvpara</c> puts the frame's first
/// baseline 144.05 pt below the top margin, and the same frame with <c>\pvpara</c> and <c>\posy0</c>
/// sits 0.05 pt below its paragraph.
/// </para>
/// </remarks>
internal readonly record struct RtfParagraphFrame
{
    /// <summary><c>\posx</c>, twips; zero for unset.</summary>
    public int X { get; init; }

    /// <summary><c>\posy</c> plus one, twips; zero for unset.</summary>
    public int Y { get; init; }

    /// <summary><c>\absw</c>, twips; zero for unset.</summary>
    public int Width { get; init; }

    /// <summary><c>\absh</c>, twips: negative for an exact height, positive for a minimum.</summary>
    public int Height { get; init; }

    /// <summary><c>\dfrmtxtx</c> or <c>\dxfrtext</c>, twips.</summary>
    public int HorizontalSpace { get; init; }

    /// <summary><c>\dfrmtxty</c> or <c>\dxfrtext</c>, twips.</summary>
    public int VerticalSpace { get; init; }

    /// <summary><c>\posxl</c> and its siblings; null when none was stated or <c>\posx</c> came later.</summary>
    public ParagraphFrameHorizontalAlignment? XAlign { get; init; }

    /// <summary><c>\posyt</c> and its siblings; null when none was stated or <c>\posy</c> came later.</summary>
    public ParagraphFrameVerticalAlignment? YAlign { get; init; }

    /// <summary>True for <c>\posyil</c>, which writerfilter reads as "not a frame at all".</summary>
    public bool IsInline { get; init; }

    /// <summary><c>\phcol</c>, <c>\phmrg</c>, <c>\phpg</c>.</summary>
    public ParagraphFrameRelation? HorizontalAnchor { get; init; }

    /// <summary><c>\pvpara</c>, <c>\pvmrg</c>, <c>\pvpg</c>.</summary>
    public ParagraphFrameRelation? VerticalAnchor { get; init; }

    /// <summary>The wrap a word stated, or null; <c>\wrapdefault</c> is <see cref="TextWrap.Optimal"/>.</summary>
    public TextWrap? Wrap { get; init; }

    /// <summary>True for <c>\wrapdefault</c>, which states a wrap without making a frame.</summary>
    public bool WrapIsDefault { get; init; }

    /// <summary>
    /// <c>RTFFrame::hasProperties</c> (:4237-4246): what makes a paragraph a frame. The spacing words and
    /// <c>\wrapdefault</c> do not (tdf#153178), and neither does a left alignment, a column anchor, an
    /// inline alignment or a margin anchor, which are the values the translation states anyway.
    /// </summary>
    public bool HasProperties
        => X != 0 || Y != 0 || Width != 0 || Height != 0
           || XAlign is { } x && x != ParagraphFrameHorizontalAlignment.Left
           || HorizontalAnchor is { } h && h != ParagraphFrameRelation.Text
           || YAlign is not null
           || VerticalAnchor is { } v && v != ParagraphFrameRelation.Margin
           || Wrap is not null && !WrapIsDefault;

    /// <summary>The frame the paragraph asks for, or null when it is not one.</summary>
    /// <remarks>
    /// <c>\posyil</c> becomes <c>w:yAlign="inline"</c>, which <c>DomainMapper</c> answers by switching
    /// frame mode off (<c>DomainMapper.cxx</c>:1106-1122), so an inline paragraph stays in the flow
    /// whatever else it states.
    /// </remarks>
    public ParagraphFrameRequest? Request()
    {
        if (!HasProperties || IsInline) return null;

        ParagraphFrameLayer layer = new()
        {
            X = X != 0 ? X : null,
            Y = Y != 0 ? Y : null,
            Width = Width != 0 ? Width : null,
            Height = Height != 0 ? Math.Abs((long)Height) : null,
            HeightRule = Height < 0
                ? ParagraphFrameHeightRule.Exact
                : Height > 0 ? ParagraphFrameHeightRule.AtLeast : ParagraphFrameHeightRule.Auto,
            HorizontalSpace = HorizontalSpace != 0 ? HorizontalSpace : null,
            VerticalSpace = VerticalSpace != 0 ? VerticalSpace : null,
            HorizontalAnchor = HorizontalAnchor ?? ParagraphFrameRelation.Text,
            VerticalAnchor = VerticalAnchor ?? ParagraphFrameRelation.Margin,
            XAlign = XAlign ?? ParagraphFrameHorizontalAlignment.None,
            YAlign = YAlign ?? ParagraphFrameVerticalAlignment.None,
            Wrap = Wrap,
        };

        return new ParagraphFrameRequest(ParagraphFrameLayer.Resolve([layer]));
    }

    /// <summary>The frame after one control word, or null when the word is not a frame word.</summary>
    /// <param name="name">The control word.</param>
    /// <param name="parameter">Its parameter.</param>
    /// <param name="inTable">
    /// True inside a table, where the four value words are dropped: <em>"Don't try to support text frames
    /// inside tables for now"</em> (<c>rtfdispatchvalue.cxx</c>:372-374).
    /// </param>
    public RtfParagraphFrame? With(string name, int? parameter, bool inTable)
    {
        int value = parameter ?? 0;

        return name switch
        {
            "absw" => inTable ? this : this with { Width = value },
            "absh" => inTable ? this : this with { Height = value },
            "posx" => inTable ? this with { XAlign = null } : this with { X = value, XAlign = null },
            "posy" => inTable
                ? this with { YAlign = null, IsInline = false }
                : this with { Y = value + 1, YAlign = null, IsInline = false },
            "posxl" => this with { XAlign = ParagraphFrameHorizontalAlignment.Left },
            "posxc" => this with { XAlign = ParagraphFrameHorizontalAlignment.Centre },
            "posxr" => this with { XAlign = ParagraphFrameHorizontalAlignment.Right },
            "posxi" => this with { XAlign = ParagraphFrameHorizontalAlignment.Inside },
            "posxo" => this with { XAlign = ParagraphFrameHorizontalAlignment.Outside },
            "posyt" or "posyin" => this with { YAlign = ParagraphFrameVerticalAlignment.Top, IsInline = false },
            "posyc" => this with { YAlign = ParagraphFrameVerticalAlignment.Centre, IsInline = false },
            "posyb" or "posyout" => this with { YAlign = ParagraphFrameVerticalAlignment.Bottom, IsInline = false },
            "posyil" => this with { YAlign = null, IsInline = true },
            "phcol" => this with { HorizontalAnchor = ParagraphFrameRelation.Text },
            "phmrg" => this with { HorizontalAnchor = ParagraphFrameRelation.Margin },
            "phpg" => this with { HorizontalAnchor = ParagraphFrameRelation.Page },
            "pvpara" => this with { VerticalAnchor = ParagraphFrameRelation.Text },
            "pvmrg" => this with { VerticalAnchor = ParagraphFrameRelation.Margin },
            "pvpg" => this with { VerticalAnchor = ParagraphFrameRelation.Page },

            // The OOXML values `rtfdispatchflag.cxx`:1248-1271 states, mapped as `DomainMapper` maps
            // them (:1146-1165): notBeside and tight leave no text beside the frame, none lets it run
            // across, around wraps both sides and through and auto take the wider side.
            "nowrap" or "wraptight" => this with { Wrap = TextWrap.TopAndBottom, WrapIsDefault = false },
            "overlay" => this with { Wrap = TextWrap.Through, WrapIsDefault = false },
            "wraparound" => this with { Wrap = TextWrap.Both, WrapIsDefault = false },
            "wrapthrough" => this with { Wrap = TextWrap.Optimal, WrapIsDefault = false },
            "wrapdefault" => this with { Wrap = TextWrap.Optimal, WrapIsDefault = true },
            "dfrmtxtx" => this with { HorizontalSpace = value },
            "dfrmtxty" => this with { VerticalSpace = value },
            "dxfrtext" => this with { HorizontalSpace = value, VerticalSpace = value },
            _ => null,
        };
    }
}
