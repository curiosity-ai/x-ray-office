# Escapement: w:position, \up/\dn, automatic rises, and line growth

Reference: the 26.8 daily under the stock fontconfig. `mk-docx-position.py` (13 DOCX), `mk-matrix.py`
(4 faces × 3 sizes × 8 ODF `style:text-position` values and 7 RTF words = 180 files), and the RTF half
converted to `.doc` by the daily itself (56 files). `esccmp.py` compares pitch above the escaped line,
the rise, and the pitch below, from `pdf-ops.py dump`.

| set | agree within 0.06 pt |
|---|---|
| DOCX `w:position` / `vertAlign` / styles | 13 of 13 (`docx-position.txt`) |
| ODT + RTF matrix | 170 of 180 |
| DOC (sprmCHpsPos, sprmCIss) | 53 of 56 |

Every residual is an *automatic* rise off by 2–3 twips (Carlito 12 sup 94 vs 97, DejaVu Serif 12 sup 92
vs 94, DejaVu Serif and Liberation Sans 8 sup 62 vs 64, Liberation Sans 20 sub −34 vs −36); every line
height and every numeric rise agrees. Summed |error| over the 24 automatic cases is 19 twips for the
ascent/descent rule against 954 for the 33 % rule it replaces, which lowered a subscript by a third of
the height (−96 twips where the reference lowers −26). The residual is not a rounding of the 58 % size
(the fitted small size varies 0.578–0.590 by face and size) and is left.
