# DOC automatic hyphenation

Reference: LibreOfficeDev 26.8.3.0.0 9dcd8a837588, stock fontconfig. `gen-dochyph.py` writes two DOCX
(with and without `w:autoHyphenation`, one paragraph suppressing it), the daily converts them to `.doc`,
and `patch-dop.py` varies one `Dop` field at a time; `dropStyleNoAutoHyph=1` renames the stylesheet's
`sprmPFNoAutoHyph` so the styles state nothing. Counted: lines ending in a hyphen, and whether every line
of the page is the reference's.

| file | reference hyphenated lines | before | after |
|---|---|---|---|
| `h-on` (style states `sprmPFNoAutoHyph` 0) | 10 | 0 | 10, all lines identical |
| `h-on`, `fAutoHyphen` cleared | 10 | 0 | 10 |
| `h-on`, `dxaHotZ` 5000 / `cConsecHypLim` 1 / `fHyphCapitals` 0 | 10 each | 0 | 10 |
| `h-off` (style states 1), `fAutoHyphen` set | 0 | 0 | 0 |
| style sprm dropped, `fAutoHyphen` set | 10 | 0 | 10 |
| style sprm dropped, `fAutoHyphen` clear | 0 | 0 | 0 |
| style sprm dropped, `fAutoHyphen` set, `dxaHotZ` 5000 | 10 | 0 | 10 |

So `fAutoHyphen` decides only where no style states `sprmPFNoAutoHyph`, and `dxaHotZ`, `cConsecHypLim` and
`fHyphCapitals` are not read, as `ww8par2.cxx`:4557-4571 and `ww8par6.cxx`:5378-5398 say. Eleven of eleven
variants are line-identical after the change.

## DOCX: `w:suppressAutoHyphens w:val="0"` without `w:autoHyphenation`

`gen-docx-stated.py`: the reference hyphenates the paragraph (2 lines end in a hyphen); the base tree drew
none, this branch draws the same lines as the reference. dmapper maps the element to
`ParaIsHyphenation = !value` whatever the settings say. Corpus: `docx/issue_359_list_whitespace.docx`
states it six times under `w:autoHyphenation w:val="0"`, but none of its lines is long enough to break,
so it renders identically.
