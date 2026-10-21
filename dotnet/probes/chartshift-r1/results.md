# The 26.8 chart shift: titles' insets, centred blanks, automatic ODF plot areas

Measured 2026-10-21 against `/opt/libreofficedev26.8/program/soffice` (`LibreOfficeDev 26.8.3.0.0
9dcd8a837588`, the 26.8 branch daily), with `/opt/libreoffice26.2` (26.2.4.2) as the explanation,
both under `FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf`. Tree at `112a3e34a`.

## 1. Every chart's plot area moved 1.82 pt: a title's insets are sized by 12 pt

`ShapeFactory::createText`, the overload `VTitle::createShapes` calls, sets the "#i109336#"
insets from `xShape->SvxShape::getPropertyValue("CharHeight")`. On `libreoffice-26-2` it set each
formatted string's character properties **on the shape** first; on `libreoffice-26-8` it appends
them to a text portion (`appendTextPortion`, `PropertyMapper::getPropVals`), so the shape keeps the
model's pool default, `SetDefaultFontHeight(423)` (`DrawModelWrapper.cxx:52`). Found by diffing the
two branch heads' `chart2/source/view` (`git fetch --filter=blob:none origin libreoffice-26-2`);
the hunk is the only one in the view that touches text-shape geometry.

So every title is inset `round(423 × 0.18) = 76` across and `round(423 × 0.30) = 127` down,
whatever its size. Read off the reference's own `chart:coordinate-region`
(`--convert-to fodp` of `slide-chart-accounting-axis.pptx`: 13 pt title, 9 pt axis titles):

| edge | 26.2.4.2 | daily | Δ (mm100) | predicted |
|---|---:|---:|---:|---:|
| left | 3.136 cm | 3.200 cm | +64 | `2 × (127 − 95)` = +64 (rotated 9 pt value title) |
| top | 1.594 | 1.572 | −22 | `2 × (127 − 138)` = −22 (13 pt main title) |
| bottom | 10.132 | 10.068 | −64 | `2 × (127 − 95)` (9 pt category title) |
| right | 19.906 | 19.906 | 0 | no title borders it |

`ChartLayout.TitleInsetEm` / `TitleShape`. After it, against the daily, over every corpus fixture
whose rendering moved (`score3.py`, word positions matched by text, max of |Δx|,|Δy|):

| fixture | mean before → after | worst before → after |
|---|---:|---:|
| `pptx/chart-bar-deck` | 1.489 → 0.106 | 2.19 → 0.99 |
| `pptx/slide-chart-accounting-axis` | 2.158 → 0.098 | 3.18 → 0.99 |
| `docx/chart-bar-text` | 0.724 → 0.060 | 1.94 → 0.14 |
| `xlsx/chart-bar-sheet` | 0.645 → 0.061 | 1.83 → 0.15 |
| `xls/sheet-chart-only-page` | 0.764 → 0.115 | 1.86 → 0.30 |
| `odp/chart-bar-deck` | 1.717 → 0.100 | 2.32 → 0.98 |
| `odt/chart-bar-text` | 0.838 → 0.051 | 2.29 → 0.11 |
| `ods/chart-bar-sheet` | 0.567 → 0.287 | 1.39 → 2.18 |
| `fods/sheet-chart-only-page` | 0.927 → 0.402 | 7.27 → 8.73 |

(15 movers in all, the other six of the same shape.) The 0.99 worst on the decks is the rotated
`Units` title, 0.98 pt off before as well. The two ODF sheets' worst is the same title: its drawn
run is 2.38 / 8.87 pt longer than the reference's in both builds (a rotated label in an
anisotropically stretched chart), so its `yMin` is not a position; centre to centre it moved
0.87 pt up exactly as the daily's did from 26.2.4.2, and the residual it now shows is the ~1 pt the
base already had against 26.2.4.2. 529 of the 544 fixtures are byte-identical.

## 2. A centred chart label is centred on its visible text

The accounting deck's value labels moved 3.18 pt, not 1.82. Four one-attribute variants
(`_)` deleted, `_(` deleted, both, as authored): the excess is present exactly when the label
ends in a blank, and it is half the blank. Same in all three hosts (`docx-acct`, `xlsx-acct`,
a value axis given the accounting code with `sourceLinked="0"`): **+1.36 to +1.37 pt**; a category
written `"Q1   "` in a document and a workbook: **+4.08 pt** against three blanks' 4.07.

The rule is tdf#168135 (`ImpEditEngine::CreateLines`, `impedit3.cxx:1646-1682` on the 26.8
branch), which centres a line on its width less trailing U+0020; every chart label is a centred
paragraph (`getTextLabelMultiPropertyLists`, `PropertyMapper.cxx:546`) in a shape autogrown to the
whole line, so the box keeps the blank and the glyphs move half of it. A legend entry
(`VLegend.cxx:118`) and a data table's series name (`DataTableView.cxx:412-413`) are left-aligned.
`ChartLayout.OnVisibleText`, run after `DrawnExtent` and before `Stretch`. After it every word of
the six variants is within 0.3 pt of the daily except the deck's `Units` above.

## 3. An automatic ODF plot area ignores its stated rectangle

`odp/chart-bar-deck` did not improve under §1 because the ODF reader took the file's
`chart:coordinate-region`, and the daily ignored it. `SchXMLPlotAreaContext::endFastElement`
applies a stated rectangle only under `!m_aOuterPositioning.isAutomatic()`, which is
`m_bAutoSize || m_bAutoPosition` from the plot area's style (`SchXMLPlotAreaContext.cxx:565-580`,
`:804-817`, `:860-868`). Four variants of the deck's `ch4` style:

| `auto-position` / `auto-size` | daily's region x |
|---|---:|
| true / true (as written) | 2.340 cm |
| true / false | 2.340 |
| false / true | 2.340 |
| false / false | **2.258** (stated) |

`OdfChartPlot.Region`. The rectangle agreed with 26.2.4.2 only because 26.2 wrote it.

## 4. The other movers between 26.2.4.2 and the daily

`movers.py 262 dev` over all 544 fixtures, word boxes > 0.01 pt (`movers-262-vs-daily.tsv`, 58
rows including 0.01 pt jitter):

- **Charts** — 15 rows, §1–§3; taken.
- **`sheet-centred-wrap-blanks.fods`** — tdf#168135 in Calc, already taken by the retarget.
- **`slide-symbol-bullet`, `odp-list-bullet-symbol`, `slides-features`, `ppt-features`,
  `deck-features`, `words-formula-complexity`** — `yMin` of an OpenSymbol glyph only, at an
  unchanged baseline: the daily resolves the system OpenSymbol (its bundled `opens___.ttf` is
  aside) and 26.2.4.2 its own, whose ascent differs. A font-file confound, not a rule;
  `odp-list-bullet-symbol` matches the daily exactly.
- **`sheet-font-unnamed`, `mixed-runs` ×5, `table-paragraph-leading`** — already right before
  this round (within 0.15 pt of the daily, unchanged).
- **`vector-picture-*`, `svg-picture`** — the daily draws the SVG `PAPERLESS` 0.61 pt wider and
  0.23 taller; this tree already draws it 0.71 / 0.25 wider and taller than 26.2.4.2, within
  0.12 pt of the daily on the decks and 0.45 in the workbooks. No rule identified.
- **`ppt-master-shapes`** — the master's date, footer and slide-number text is drawn 0.4–0.6 %
  narrower by the daily at unchanged height and anchors (`Confidential draft` 99.32 pt against
  99.95); this tree keeps 26.2.4.2's advances and matches the anchors (the right-aligned date ends
  at 124.666 against 124.668). Cause not found; left.
- **`header-table.rtf`** — the body starts 2.45 pt lower in the daily because the header's
  trailing empty paragraph after its table is imported in `Standard` (12 pt) instead of the
  `\f4\fs18` in force (`--convert-to fodt`: `P2` → `Standard`); a stated `\fs36` there is ignored
  too, while a non-empty trailing paragraph keeps its formatting. An RTF-import change, probably
  `8701886edf4` or the `\intbl` rework `77b3b688e66`/`d2e70beadd3`; this tree drops the empty
  paragraph and matches 26.2.4.2 by coincidence. Left: words track, not a chart rule.
- **The rest** (`ppt-page-fill`, `sheet-shape-group-*`, `ppt-text-flow`, `tables` ×5, …) — under
  0.07 pt.
