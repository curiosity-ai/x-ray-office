# Calc anchors, print ranges, number colours and fill alignment against 26.8.1.1

Reference: `/opt/libreoffice26.8/program/soffice` 26.8.1.1, `FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf`.

- `make-fixtures.py`: the chart `editAs="oneCell"` fixture and copies of the rot90 and
  hidden-print-area probes. `redbox.py <pdf> [dpi]` reads the red bar's box.
  rot90 at 144 dpi: 26.8.1.1 157x472 at (301,204); this branch the same; before 384x157.
- `numcolour2.fods`: seven data-style colours. 26.8.1.1 paints #2a6099 (entry 21) and #81d41a
  (24), leaves #123456 black, and prints #3465a4 (81) and #342a06 (120) in General
  (`1001.5`), because `[COLOR n]` is read only for 1..64. `rowcolours.py` reads the colours.
- `make-xlcolour.py`: the same rule for an xlsx code.
- `make-fill.py`: `fill2.xlsx` (151 fill cells) and the test fixture. `fillsim.py` fits the
  repeat count's unit against the reference (mm100 for room and rounded text width: 148 of 151;
  720 dpi px: 134; with the printer pixel: 94); `fillcount.py ref.pdf ours.pdf` compares cells.
