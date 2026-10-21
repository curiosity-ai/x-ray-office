#!/usr/bin/env python3
"""cmp.py ref.pdf ours.pdf : per-line first-word top (yMin) on both sides, and the difference."""
import re, subprocess, sys
def words(pdf):
    x = subprocess.run(["pdftotext", "-bbox", pdf, "-"], capture_output=True, text=True).stdout
    out = {}; page = 0
    for line in x.splitlines():
        if "<page " in line: page += 1
        m = re.search(r'xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">([^<]+)<', line)
        if m: out[m.group(5)] = (page, float(m.group(1)), float(m.group(2)), float(m.group(4)))
    return out
r, o = words(sys.argv[1]), words(sys.argv[2])
prev = None; worst = 0
for w in sorted(r, key=lambda k: (r[k][0], r[k][2], r[k][1])):
    if not re.match(r"^[A-Z]w\d\d$", w): continue
    pg, x, y, y2 = r[w]
    key = (w[0], round(y, 1), pg)
    if key == prev: continue
    prev = key
    if w in o:
        d = o[w][2] - y if o[w][0] == pg else float("nan")
        worst = max(worst, abs(d)) if d == d else worst
        print(f"{w}  p{pg} ref {y:8.2f} ours p{o[w][0]} {o[w][2]:8.2f}  d {d:+6.2f}")
    else:
        print(f"{w}  p{pg} ref {y:8.2f} ours MISSING")
print("worst", round(worst, 2))
