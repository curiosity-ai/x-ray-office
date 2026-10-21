# RTF paragraph borders and shading

Reference: the 26.8 daily, `/opt/libreofficedev26.8/program/soffice` (LibreOfficeDev 26.8.3.0.0
9dcd8a837588), under `FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf`. Probes: `mk.py`, `mk2.py`,
`mk3.py`, `mk4.py` (one RTF per case, Liberation Serif 12 pt, 1 inch margins); `both.sh` renders each
both ways and `geo.py` lists text origins and rules from `pdf-ops.py dump`. `after.txt` is the final
table: every text origin agrees to the writers' constant 0.10 pt, every rule to 0.05 pt.

Reach: **none in `test_documents` (17 RTF) or in `tests/corpus` (39 RTF)** — no file states a
paragraph border or `\cbpat`. The sample corpus holds no RTF.

## Rules measured

| case | reference | rule |
|---|---|---|
| `\brdrt\brdrs\brdrw10` | Bravo 0.50 lower | `\brdrw` twips, via writerfilter's eighths round trip |
| `+\brsp80` | 4.50 lower | `\brsp` whole points (twips / 20, truncated) |
| `\brdrb\brdrw30` (no style) | nothing | no style word, no line |
| `\brdrb\brdrs` | 0.75 | unstated width is 15 twips |
| `\brdrb\brdrdb\brdrw15` | 2.25 | `BorderRules.FromWord` |
| `\box…\brsp60` | text at 76.10 vs 72.10; rules at the margins | box INSIDE the indents (no `InvertBorderSpacing` for RTF) |
| two alike boxes | Delta one pitch below Bravo | Writer join |
| `\brdrbtw` both | gap = upper bottom `\brsp` + between width + between `\brsp` (3+0.5+0 / 3+0.5+5 / 1+0.5 / 5+0.5) | dmapper's between-as-top emulation |
| box / between-box | joined | join asks the UPPER's connect flag |
| between-box / box | not joined, 8 pt | same |
| `\brdrt` + lineless `\brdrb\brsp100` | next paragraph 5 pt lower | distance counts beside a drawn side |
| lineless `\brdrb\brsp100` alone | nothing | no line at all, no room |
| `\cbpat3` | fill over the line stack | |
| `\cbpat3\box` | fill over the whole box, rules included | |
| `\tx2880` + left rule | tab stays at 216.10 | tabs absolute, indent narrowed |
| style `\s1\brdrb…` | nothing | a style's own border does not reach the text (like its `\sb`) — not read |
