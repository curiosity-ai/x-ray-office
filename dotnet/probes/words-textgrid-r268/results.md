# Text grid and the East Asian line height on 26.8.1.1

Reference: `/opt/libreoffice26.8/program/soffice` (26.8.1.1), `FONTCONFIG_FILE` = the stock
fontconfig. Ours: branch `xo-docx`, `xray-office render`. Measured 2026-10-21.

## `w:docGrid` (gen-grid.py, cmp.py)

Fourteen paragraphs (single, 24 pt, 150 %, at-least 0, at-least 500, exact 300, empty 11 pt and 30 pt,
`w:snapToGrid w:val="0"`, WenQuanYi Zen Hei, 75 %) and a table holding a 150 % paragraph and a
WenQuanYi one, in five section variants. Worst |Δ| of each line's first word's top against the
reference:

| variant | before | after |
|---|---:|---:|
| no grid (`w:linePitch` only) | 1.81 | 0.05 |
| `lines` 360 | 60.45 | 0.01 |
| `linesAndChars` 312 (no `charSpace`) | 36.45 | 0.01 |
| `lines` 360 + `w:adjustLineHeightInTable` | 79.95 | 0.01 |
| `lines` 200 | 53.45 | 0.01 |

The "no grid" row moves only on its two WenQuanYi paragraphs: that is the 127 % rule below.

What the reference does, all from `SwTextFormatter::CalcRealHeight`'s grid branch
(`sw/source/core/text/itrform2.cxx`:2275-2345) unless said otherwise:

- a line is `pitch * ceil(height / pitch)`, the text centred: 11 pt Liberation Serif (253 twips) sits
  53 twips lower in an 18 pt pitch;
- at-least 0 is the text's own height, at-least N raises to N, exact N replaces (tdf#164871);
- a proportional rule reaches only second and later lines, never below 100 %;
- an empty, start-aligned paragraph is exactly one pitch (`SwTextFrame::FormatEmpty`,
  `porrst.cxx`:489-494): an empty 30 pt paragraph is 18 pt, not 36;
- the leading handed to the next paragraph is the proportional percentage of the last line's *text*
  height (`SwTextFrame::GetLineSpace`, grid-blind): 6.30 pt after a 150 % 11 pt paragraph;
- a table cell's paragraph takes no line spacing at all unless `w:adjustLineHeightInTable`
  (tdf#88752, tdf#167583), and still hands its leading on;
- headers, footers, notes and text boxes never snap (`IsInDocBody`, `inftxt.cxx`:334).

`docx/textbox.docx` (`lines` 360): mean |dy| over 243 matched words 44.96 -> 0.21 pt, 226 within
0.1 pt. The rest is a 2.2 pt offset of one text box and list-indent x offsets, not investigated here.

## The 127 % East Asian height (probe-cjk127-268.py, gen-empty-cjk.py)

`probe-cjk127-268.py` scores 26.8.1.1's form at 117 of 117 ascents and heights; this tree's
rendering of the same document is within 0.02 pt on all 117 pages (before: up to 0.76).

Empty paragraphs are not scaled on 26.8.1.1 (no text portion for `CalcAscent`): an empty 12 pt
WenQuanYi Zen Hei paragraph is 16.25 pt, a line of text 20.60. `gen-empty-cjk.py`: worst 20.30 pt ->
0.01 pt.
