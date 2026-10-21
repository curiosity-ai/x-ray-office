#!/usr/bin/env python3
"""pitch.py a.pdf...: per paragraph line, chars on it, and the CJK x0 steps (distinct values, with counts)."""
import sys, collections, pdfplumber
for pdf in sys.argv[1:]:
    print("==", pdf)
    with pdfplumber.open(pdf) as p:
        for page in p.pages:
            rows = {}
            for c in page.chars:
                if c["text"].strip(): rows.setdefault(round(c["top"]), []).append(c)
            for top in sorted(rows):
                cs = sorted(rows[top], key=lambda c: c["x0"])
                cj = [c for c in cs if ord(c["text"]) > 0x2e80]
                steps = collections.Counter(round(b["x0"] - a["x0"], 2) for a, b in zip(cj, cj[1:]))
                t = "".join(c["text"] for c in cs)
                print(f"  y{top:6.1f} n{len(cs):3d} x0 {cs[0]['x0']:7.2f} x1 {cs[-1]['x1']:7.2f} {t[:10]:12s} steps {dict(steps.most_common(3))}")
