# RTF `\hyphpar` on a style

Reference: LibreOfficeDev 26.8.3.0.0 9dcd8a837588, stock fontconfig. `gen-hyphpar.py`, one paragraph per
document in style `\s1` (based on `\s0`) or `\s2` (based on `\s1`).

| case | reference hyphenated lines | before | after |
|---|---|---|---|
| `\hyphauto1`, no style | 4 | 4 | 4 |
| `\hyphauto1`, `\s1` states `\hyphpar0` | 0 | 4 | 0 |
| the same, paragraph in `\s2` | 0 | 4 | 0 |
| the same, paragraph adds `\hyphpar` | 4 | 4 | 4 |
| no `\hyphauto`, paragraph `\hyphpar` | 4 | 0 | 4 |
| no `\hyphauto`, `\s1` states `\hyphpar` | 4 | 0 | 4 |
| `\hyphauto1`, `\s1` states `\hyphpar` | 4 | 4 | 4 |

Seven of seven line-identical after the change. The last two rows of "no `\hyphauto`" are the larger
finding: `\hyphpar` is `suppressAutoHyphens`, which DomainMapper maps to `ParaIsHyphenation = !value`
whatever the document says. No document in `test_documents` states `\hyphpar`.
