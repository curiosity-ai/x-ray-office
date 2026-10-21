#!/usr/bin/env python3
"""edges.py dir : per case, first-line start, second-line start, widest line's end (as its last word), and
the top of a paragraph tagged B, for ref/<case>.pdf and ours/<case>.pdf side by side."""
import os, re, subprocess, sys
def lines(pdf):
    x = subprocess.run(["pdftotext", "-bbox", pdf, "-"], capture_output=True, text=True).stdout
    rows = {}
    for m in re.finditer(r'xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="[\d.]+">([^<]+)<', x):
        rows.setdefault(round(float(m.group(2)), 0), []).append((float(m.group(1)), float(m.group(3)), m.group(4)))
    return [sorted(rows[k]) for k in sorted(rows)]
def summary(pdf):
    if not os.path.exists(pdf): return "missing"
    ls = lines(pdf)
    if not ls: return "empty"
    b = next((y for y, l in zip(sorted({}), ls) ), None)
    first = ls[0][0][0]; second = ls[1][0][0] if len(ls) > 1 else float('nan')
    right = max(l[-1][1] for l in ls[:-1]) if len(ls) > 1 else ls[0][-1][1]
    return f"n{len(ls):2d} first {first:7.2f} second {second:7.2f} right {right:7.2f} last1 {ls[0][-1][2]}"
d = sys.argv[1]
for f in sorted(os.listdir(os.path.join(d, "ref"))):
    print(f"{f[:-4]:34s} ref  {summary(os.path.join(d, 'ref', f))}")
    print(f"{'':34s} ours {summary(os.path.join(d, 'ours', f))}")
