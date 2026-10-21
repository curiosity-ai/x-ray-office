# A table style's `w:contextualSpacing` on 26.8.1.1

`../words-textgrid-r268/mkdocx.py ctx.docx body.xml styles.xml settings.xml`, rendered by 26.8.1.1
(stock fontconfig) and by this tree; first-word tops via `../words-textgrid-r268/cmp.py`.

| word | 26.8.1.1 | this tree |
|---|---:|---:|
| `Aw00` (cell, 1st) | 72.45 | 72.45 |
| `Bw00` (cell, 2nd) | 97.10 | 85.10 |
| `Cw00` (cell, 3rd) | 121.75 | 97.75 |
| `Dw00` (after the table) | 146.40 | 122.40 |

26.8.1.1 keeps the style's 12 pt after between the cell's same-style paragraphs: the table style's
contextual spacing does not reach them (`5dc72823c12d`, master only). This tree follows Word; see
`TODO.word-parity.md`.
