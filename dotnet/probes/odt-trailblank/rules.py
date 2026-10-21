#!/usr/bin/env python3
"""rules.py a.pdf...: every thin horizontal rule (underline, strikeout) as x0..x1 at its y, rounded."""
import sys, pdfplumber
for pdf in sys.argv[1:]:
    with pdfplumber.open(pdf) as p:
        rs = [r for r in p.pages[0].rects + p.pages[0].lines if abs(r["bottom"] - r["top"]) < 2.5]
        print(pdf, " ".join(f"{r['top']:.0f}:{r['x0']:.1f}-{r['x1']:.1f}" for r in sorted(rs, key=lambda r: (r['top'], r['x0']))))
