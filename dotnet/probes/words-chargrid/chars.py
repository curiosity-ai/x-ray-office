#!/usr/bin/env python3
"""chars.py a.pdf [b.pdf]: per text line, its top, its first characters' x0 and the line's last x1.

With two PDFs, prints them side by side and the largest x difference over matched characters."""
import sys, pdfplumber
def lines(pdf):
    out = []
    with pdfplumber.open(pdf) as p:
        for pg, page in enumerate(p.pages, 1):
            rows = {}
            for c in page.chars:
                if not c["text"].strip(): continue
                rows.setdefault(round(c["top"]), []).append(c)
            for top in sorted(rows):
                cs = sorted(rows[top], key=lambda c: c["x0"])
                out.append((pg, top, cs))
    return out
A = lines(sys.argv[1]); B = lines(sys.argv[2]) if len(sys.argv) > 2 else None
worst = 0
for i, (pg, top, cs) in enumerate(A):
    t = "".join(c["text"] for c in cs)
    print(f"p{pg} y{top:6.1f} n{len(cs):3d} x0 {cs[0]['x0']:7.2f} x1 {cs[-1]['x1']:7.2f} {t[:24]}")
    xs = " ".join(f"{c['x0']:.2f}" for c in cs[:6])
    if B is not None and i < len(B):
        pg2, top2, cs2 = B[i]
        t2 = "".join(c["text"] for c in cs2)
        same = t2 == t
        d = max((abs(a["x0"] - b["x0"]) for a, b in zip(cs, cs2)), default=0) if same else float("nan")
        if same: worst = max(worst, d)
        print(f"   ours y{top2:6.1f} n{len(cs2):3d} x0 {cs2[0]['x0']:7.2f} x1 {cs2[-1]['x1']:7.2f} {t2[:24]}  {'dx ' + format(d, '.2f') if same else 'TEXT DIFFERS'}")
    else:
        print("   " + xs)
if B is not None: print("lines", len(A), len(B), "worst dx", round(worst, 2))
