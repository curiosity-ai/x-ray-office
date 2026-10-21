#!/usr/bin/env python3
"""RTF \\hyphpar at style level: one RTF per case. Writes r*.rtf into the directory given as argument."""
import os, sys
OUT = sys.argv[1]
TEXT = ("Internationalisation of extraordinarily complicated administrative responsibilities "
        "characteristically necessitates uncharacteristically comprehensive documentation and "
        "interdisciplinary collaboration between representatives of incomprehensibly bureaucratic "
        "organisations, notwithstanding considerable counterproductive misunderstandings.")
def rtf(settings, s1, para):
    return ("{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0\\froman Liberation Serif;}}"
            "{\\stylesheet{\\s0\\f0\\fs22\\lang1033 Normal;}{\\s1\\sbasedon0" + s1 + "\\f0\\fs22\\lang1033 Body;}"
            "{\\s2\\sbasedon1\\f0\\fs22\\lang1033 Child;}}"
            "\\paperw12240\\paperh15840\\margl1440\\margr5040\\margt1440\\margb1440" + settings +
            "\\pard\\plain" + para + "\\f0\\fs22\\lang1033 " + TEXT + "\\par}")
CASES = {
    "r0-auto-plain": ("\\hyphauto1", "", ""),
    "r1-auto-style0": ("\\hyphauto1", "\\hyphpar0", "\\s1"),
    "r2-auto-style0-child": ("\\hyphauto1", "\\hyphpar0", "\\s2"),
    "r3-auto-style0-para1": ("\\hyphauto1", "\\hyphpar0", "\\s1\\hyphpar"),
    "r4-noauto-para1": ("", "", "\\hyphpar"),
    "r5-noauto-style1": ("", "\\hyphpar", "\\s1"),
    "r6-auto-style1": ("\\hyphauto1", "\\hyphpar", "\\s1"),
}
for name, (settings, s1, para) in CASES.items():
    open(os.path.join(OUT, name + ".rtf"), "w").write(rtf(settings, s1, para))
print("ok")
