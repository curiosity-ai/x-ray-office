# The text grid's character half (DOCX and DOC)

Reference: `/opt/libreofficedev26.8/program/soffice` (LibreOfficeDev 26.8.3.0.0 9dcd8a837588), run with
`FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf`. Ours: branch `xo-gridhyph`, `xray-office render`.
Measured 2026-10-21. Instruments: `chars.py ref.pdf ours.pdf` (per text line, the characters on it and
the largest x0 difference over the line's characters; `TEXT DIFFERS` means the line broke elsewhere) and
`pitch.py` (the x0 steps between consecutive East Asian characters).

`gen-chargrid.py` writes ten DOCX, one per `w:docGrid` variant, nine paragraphs each: 45 ideographs after
`P1`, Latin prose, CJK punctuation `「」、。，`, mixed Latin/CJK, 16 pt ideographs, a justified CJK
paragraph, `w:snapToGrid w:val="0"`, a justified Latin line with a 300 pt word after it, and a justified
mixed paragraph. Default font 10.5 pt, East Asian face WenQuanYi Zen Hei.

Lines whose text differs from the reference's line (`chars.py`'s `TEXT DIFFERS`), base tree `cd9e490bc`
against this branch:

| variant | reference CJK step | lines broken differently, before -> after |
|---|---|---|
| `lines` 312 | 10.5 | 0 -> 0 |
| `linesAndChars`, `charSpace` 0 | 10.5 (downgraded to lines) | 0 -> 0 |
| `linesAndChars` -3426 | 9.65 (grid 193 tw, -17 per char) | 6 -> 0 |
| `linesAndChars` 4096 | 11.49 (230 tw, +20) | 6 -> 0 |
| `linesAndChars` 40960 | 20.49 (410 tw, +200) | 20 -> 0 |
| `linesAndChars` 4096, `Normal` 12 pt | 12.98 (260 tw over a 240 base) | 4 -> 0 |
| `linesAndChars` 2048 | 10.99 (220 tw, +10) | 6 -> 2 |
| `snapToChars` 4096 | 11.49 per cell, glyph centred (+0.5), `「` +1.0 | 14 -> 0 |
| `snapToChars` -3426 | two cells of 9.65 (19.29), glyph +4.4, `「` +8.8 | 20 -> 0 |
| `snapToChars` 0 | as lines | 0 -> 0 |

The two left on `linesAndChars` 2048 are one break in the justified mixed paragraph `P9`, where this tree
fits one more ideograph: its three blanks after East Asian text are 1.35 pt narrower than the reference's
(the blank-face residual below), which on that line is the margin. Inferred from the arithmetic, not
isolated.

The steps are read off the PDF's glyph positioning, which LibreOffice truncates to thousandths of an em,
so 11.49 is 11.5 and 12.98 is 13.0 (see the advance note in `CLAUDE.md`). After the change the largest
x0 difference on any line whose characters are all East Asian is 0.43 pt over 40 characters, the same
truncation. The residuals that remain are not the grid and are present on the `lines` variant too:

- a blank after an East Asian character is drawn by the reference in WenQuanYi Zen Hei (about 61.5 tw at
  10.5 pt) and by us in the paragraph's Latin face (52.5): 0.45 pt per such blank (`P4`);
- the reference justifies a CJK line by spreading the slack over its characters; we stretch blanks only,
  so a justified line of ideographs ends 1.9 to 5.7 pt short (`P6`, `P9`).

The justification rule under `snapToChars` is confirmed by `P8`: the reference draws that line at its
natural width (word starts 85.78, 109.36, 137.64 against ours unjustified 85.71, 109.34, 137.63), where
the `lines` and `linesAndChars` variants stretch it to the margin.

## DOC

Each DOCX above, and three of `words-textgrid-r268`'s, converted to `.doc` by the daily (its WW8 export
writes `sprmSClm`, `sprmSDyaLinePitch`, `sprmSDxtCharSpace`), and three patched copies (`patch-sep.py`):
`snapToChars` with `dxtCharSpace` 0, `linesAndChars` with 0, and a 50-twip pitch.

| file | lines broken differently, before -> after |
|---|---|
| `linesAndChars` -3426 / 4096 (`.doc`) | 6 -> 0 / 6 -> 0 |
| `snapToChars` 4096 / -3426 (`.doc`) | 14 -> 0 / 20 -> 0 |
| `snapToChars`, `dxtCharSpace` patched to 0 | 2 -> 0 (a character grid at the document's own size: 16 pt ideographs take two cells) |
| `linesAndChars`, `dxtCharSpace` patched to 0 | 0 -> 0 |
| `g1-lines360.doc` / `g3-lines360-adjust.doc` / `g4-lines200.doc` | worst line top 60.15 / 79.05 / 53.15 pt -> 0.01 / 0.01 / 0.01 |

The line-grid rows needed a second fix: the WW8 export writes an at-least of nought as a stated
`dyaLine` of 0, which this tree read as single spacing; on a grid that is the text's own height.

## Minimum pitch (DOCX)

`g4-lines200.docx` with `w:linePitch` 50 and 150: base tree 7.51 pt worst, now 0.01 on both, the 50 being
raised to `MIN_TEXTGRID_SIZE` 100.
