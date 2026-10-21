namespace XRay.Office.WordProcessing.Rtf;

/// <content>A paragraph's borders and shading.</content>
/// <remarks>
/// <para>
/// RTF states a border in two halves wherever it states one: a selector — <c>\brdrt</c>, <c>\brdrl</c>,
/// <c>\brdrb</c>, <c>\brdrr</c>, <c>\brdrbtw</c> or <c>\box</c> for a paragraph — and then the describing
/// words <c>\brdrw</c>, <c>\brdrcf</c>, <c>\brsp</c> and one style word, which apply to whatever was selected
/// last. writerfilter keeps that selection as <c>RTFParserState::m_nBorderState</c>, set by the selectors of
/// all five scopes (<c>rtfdispatchflag.cxx</c>:643-801) and consulted by <c>putBorderProperty</c>
/// (<c>rtfdocumentimpl.cxx</c>:167-215). The page's scope is <see cref="RtfPageGeometry"/>'s and is
/// dispatched first; a cell's is <c>TableLevel.PendingBorderSide</c>; this is the paragraph's.
/// </para>
/// <para>
/// <c>\brdrbar</c> has no dispatch in writerfilter at all — it is in the tokeniser's table and nowhere else
/// — so it neither draws nor moves the selection, and is left to the default arm. <c>\shading</c> and
/// <c>\cfpat</c> are the same for a paragraph: only <c>\cbpat</c> reaches one
/// (<c>rtfdispatchvalue.cxx</c>:923-930), as its fill colour, and only when it is not zero.
/// </para>
/// </remarks>
public sealed partial class RtfDocumentReader
{
    /// <summary>Handles one paragraph border or shading word, answering whether it was one.</summary>
    private bool HandleParagraphBorderWord(RtfToken token, GroupState state)
    {
        switch (token.Name)
        {
            case "brdrt": SelectParagraphBorder(state, 0); return true;
            case "brdrl": SelectParagraphBorder(state, 1); return true;
            case "brdrb": SelectParagraphBorder(state, 2); return true;
            case "brdrr": SelectParagraphBorder(state, 3); return true;
            case "brdrbtw": SelectParagraphBorder(state, 4); return true;

            case "box":
            {
                // One value shared by all four sides (`rtfdispatchflag.cxx`:643-651), so every describing
                // word that follows lands on each of them.
                RtfBorderDraft side = default;
                state.ParagraphBorders = state.ParagraphBorders with
                {
                    Top = side, Left = side, Bottom = side, Right = side,
                };
                state.ParagraphBorderSide = RtfBorderDrafts.Box;
                ClearCellBorderSelection();
                return true;
            }

            // Another scope's selector takes the describing words from here on.
            case "clbrdrt" or "clbrdrl" or "clbrdrb" or "clbrdrr"
                or "pgbrdrt" or "pgbrdrl" or "pgbrdrb" or "pgbrdrr"
                or "trbrdrt" or "trbrdrl" or "trbrdrb" or "trbrdrr" or "trbrdrh" or "trbrdrv"
                or "chbrdr":
                state.ParagraphBorderSide = null;
                return false;

            case "cbpat":
                if (token.Parameter is { } index and not 0) state.ShadingColourIndex = index;
                return true;

            default:
                break;
        }

        if (state.ParagraphBorderSide is not { } selected) return false;

        switch (token.Name)
        {
            case "brdrw":
                Describe(state, selected, side => side with { Twips = token.Parameter ?? 0 });
                return true;
            case "brdrcf":
                Describe(state, selected, side => side with { ColourIndex = token.Parameter });
                return true;
            case "brsp":
                Describe(state, selected, side => side with { SpaceTwips = token.Parameter ?? 0 });
                return true;
            case "brdrsh":
                // A shadow is the paragraph's, and not drawn by this layout; consumed so it describes
                // nothing else.
                return true;
            default:
                break;
        }

        if (RtfPageGeometry.StyleOf(token.Name) is not { } style) return false;

        Describe(state, selected, side => side with { Style = style });
        return true;
    }

    /// <summary>Starts a fresh side, as <c>putNestedSprm</c> overwrites the side's value.</summary>
    private void SelectParagraphBorder(GroupState state, int side)
    {
        state.ParagraphBorders = state.ParagraphBorders.With(side, default(RtfBorderDraft));
        state.ParagraphBorderSide = side;
        ClearCellBorderSelection();
    }

    /// <summary>Applies a describing word to the side, or to all four after <c>\box</c>.</summary>
    /// <remarks>
    /// A side \pard has since cleared takes nothing: writerfilter's <c>getLastAttributes</c> then finds no
    /// border sprm and writes the attribute where nothing reads it (<c>rtfdocumentimpl.cxx</c>:156-165).
    /// </remarks>
    private static void Describe(GroupState state, int selected, Func<RtfBorderDraft, RtfBorderDraft> change)
    {
        RtfBorderDrafts drafts = state.ParagraphBorders;

        if (selected == RtfBorderDrafts.Box)
        {
            for (int side = 0; side < 4; side++)
            {
                if (drafts[side] is { } draft) drafts = drafts.With(side, change(draft));
            }
        }
        else if (drafts[selected] is { } draft)
        {
            drafts = drafts.With(selected, change(draft));
        }

        state.ParagraphBorders = drafts;
    }

    /// <summary>A paragraph selector ends a cell's, whose describing words would otherwise still apply.</summary>
    private void ClearCellBorderSelection()
    {
        Flow flow = CurrentFlow;
        int level = Math.Max(1, flow.DefinitionLevel);
        if (flow.Levels.Count >= level) flow.Levels[level - 1].PendingBorderSide = null;
    }
}
