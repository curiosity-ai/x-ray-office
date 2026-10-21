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
- `fontlist.fodt` / `fontlist.fods`: nine family lists (tdf#108243). Faces in 26.8.1.1's PDF, in
  paragraph order (style:font-name then fo:font-family): LiberationMono LiberationSerif DejaVuSans
  Carlito Carlito LiberationSerif LiberationMono DejaVuSerif DejaVuSansMono / LiberationMono
  LiberationSerif DejaVuSans Carlito Carlito LiberationSerif LiberationMono DejaVuSans
  DejaVuSansMono. This branch: the same except the eighth (DejaVuSans), because the ODT reader
  does not pass `style:font-family-generic` to the resolver at all (true of single names too;
  26.2.4.2 answers DejaVuSerif there as well). 26.2.4.2 answered DejaVuSans for A, D, G and I.
- `cff.fodt`: Loma (name-keyed CFF), Inter, Unifont (CID-keyed), Inter Light. 26.8.1.1: all four
  embedded Type 1 (converted). Before: three CFF faces named, not embedded (and Inter Light
  missed, drawn in DejaVu Sans). After: Loma, Inter, Unifont embedded as CID Type 0C, poppler
  draws the same glyphs as the reference; `pdftotext` unchanged.
- `cond.fodt`: `DejaVu Sans` and `DejaVu Sans Condensed`, rendered with a fontconfig file that adds
  a directory holding copies of the eight DejaVu*Condensed faces (and `HOME` pointing at it for
  this tree's index). 26.8.1.1: DejaVuSans, DejaVuSansCondensed. Before: DejaVuSansCondensed for
  both. After: DejaVuSans, DejaVuSansCondensed. `cff.fodt`'s Inter Light: Inter-Light after.
