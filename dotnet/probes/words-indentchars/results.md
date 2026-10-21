# Indents in characters, spacing in lines (DOCX)

Reference: LibreOfficeDev 26.8.3.0.0 9dcd8a837588, stock fontconfig. `gen-indent.py` writes one document
per case (default 10.5 pt, so a character is 21 pt for two); `edges.py` prints the first line's start,
the second's, and the widest line's end. Measured 2026-10-21.

All 19 paragraph cases, 5 table-style cases and 6 spacing cases agree with the reference after the
change to the two writers' constant 0.10 pt origin offset. Before it, 15 of the 30 differed: the nine stating a non-zero count, the three table-style ones
that state a count, and the three line-unit spacings.

| case | reference | before | after |
|---|---|---|---|
| `leftChars="200"` | 93.10 | 72.00 | 93.00 |
| `leftChars="200" left="720"` | 93.10 (count wins) | 108.00 | 93.00 |
| `leftChars="0" left="720"` | 108.10 (nought disables) | 108.00 | 108.00 |
| `rightChars="0" right="374"`, either order | right indent 374 kept | kept | kept |
| `firstLineChars="200"` | first 93.10 | 72.00 | 93.00 |
| `hangingChars="100" left="720"` | first 72.10, rest 82.60 (tdf#167721) | 108.00 / 108.00 | 72.00 / 82.50 |
| `leftChars="200"`, 16 pt run | 93.10 (the style's size, not the run's) | 72.00 | 93.00 |
| `leftChars="200"`, 20 pt style | 112.10 | 72.00 | 112.00 |
| style `leftChars="300" left="200"` | 103.60 | 82.00 | 103.50 |
| that style, paragraph `left="720"` | 103.60 (inherited count wins) | 108.00 | 103.50 |
| that style, paragraph `leftChars="0"` | 82.10 (the style's twips) | 82.00 | 82.00 |
| table style `rightChars="0" right="374"` | **no right indent** | 374 | 0 |
| paragraph style, same | 374 kept | 374 | 374 |
| table style `rightChars="200"` | 420 tw | 374 | 420 |
| `beforeLines="100"` | +12 pt | 0 | +12 |
| `beforeLines="33"` | +3.95 pt (79 tw) | 0 | +3.95 |
| `before="0"` with `beforeLines="100"`, either order | 0 | 0 | 0 |
| `afterLines="50"` | +6 pt | 0 | +6 |

**"`w:rightChars="0"` zeroes the right indent" is true only in a table style.** In a paragraph or a
paragraph style a nought count disables the count and the twips apply
(`DomainMapper_Impl.cxx`:3141-3236, `StyleSheetTable.cxx`:1294-1405 drop it); a table style's properties
reach the cell without that fix-up, so the unit property wins at nought. `docx/word_sample.docx`'s
`Report Table` states it on its `firstCol` conditional: its right-aligned first column ended 18.85 pt
short of the reference and now ends 0.15 pt from it.
