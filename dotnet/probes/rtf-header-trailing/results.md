# The paragraph after a table that ends an RTF header

Variants of `tests/corpus/features/header-table.rtf`, one ending each, rendered by the 26.8 daily,
26.8.1.1 and 26.2.4.2 under the stock fontconfig. Figure: the body's first baseline (PDF y).

| header ends | daily | 26.8.1.1 | 26.2.4.2 | ours before | ours after |
|---|---:|---:|---:|---:|---:|
| `\row\pard }` (fixture) | 749.89 | 749.89 | 752.34 | 752.35 | 749.90 |
| `\row\pard\fs36 }` | 749.89 | 749.89 | 741.69 | 752.35 | 749.90 |
| `\row\pard\plain\fs36 }` | 749.89 | 749.89 | 742.99 | 752.35 | 749.90 |
| `\row\pard\s20\fs36 }` | 749.89 | 749.89 | 741.69 | 752.35 | 749.90 |
| `\row}` | 749.89 | 749.89 | 752.34 | 752.35 | 749.90 |
| `\row\pard X}` | 752.34 | 752.34 | 752.34 | 752.35 | 752.35 |
| `\row\pard\fs36 X}` | 741.69 | 741.69 | 741.69 | 741.70 | 741.70 |
| `\row\pard\par}` | 752.34 | 752.34 | 752.34 | 752.35 | 752.35 |
| `\row\pard\fs36\par}` | 741.69 | 741.69 | 741.69 | 741.70 | 741.70 |
| no table, `\par\pard\fs36 }` | 752.34 | | 752.34 | 752.35 | 752.35 |

The daily's flat export writes the trailing paragraph as `<text:p text:style-name="Standard"/>`;
26.2.4.2's as `P2` (Carlito 9 pt). Header: one 9 pt Carlito row (220 twips) plus that paragraph against a
0.3102 in (447 twip) minimum: 220 + 276 = 496 overflows by 49 twips = 2.45 pt; 220 + 220 does not.
The change is already in 26.8.1.1, so it is in the branch before the daily (candidate commits
`77b3b688e66`, `d2e70beadd3`, `8701886edf4`; the mechanism in writerfilter was not isolated).
