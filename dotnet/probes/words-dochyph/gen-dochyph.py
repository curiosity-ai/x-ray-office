#!/usr/bin/env python3
"""DOC hyphenation probe, stage 1: DOCX sources that 26.8's daily converts to .doc (its WW8 export writes
fAutoHyphen from the default paragraph style, and sprmPFNoAutoHyph per paragraph). Stage 2 is patch-dop.py.

Writes h-*.docx into the directory given as the first argument."""
import os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1]
MK = os.path.join(HERE, "mkdocx.py")
TEXT = ("Internationalisation of extraordinarily complicated administrative responsibilities "
        "characteristically necessitates uncharacteristically comprehensive documentation and "
        "interdisciplinary collaboration between representatives of incomprehensibly bureaucratic "
        "organisations, notwithstanding considerable counterproductive misunderstandings.")
SHORT = " ".join(["baby lazy tidy also into upon city duty copy body"] * 6)

def para(tag, text, ppr=""):
    return f'<w:p><w:pPr>{ppr}<w:spacing w:before="0" w:after="0"/></w:pPr><w:r><w:t xml:space="preserve">{tag} {text}</w:t></w:r></w:p>'

body = (para("A", TEXT) + para("B", TEXT, '<w:suppressAutoHyphens/>') + para("C", TEXT) + para("D", SHORT)
        + '<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="5040" w:bottom="1440" '
          'w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>')
styles = ('<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/>'
          '<w:sz w:val="22"/><w:lang w:val="en-US"/></w:rPr></w:rPrDefault></w:docDefaults>'
          '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>')
for name, settings in {"h-on": '<w:autoHyphenation/>', "h-off": ''}.items():
    d = os.path.join(OUT, name + ".src"); os.makedirs(d, exist_ok=True)
    for f, c in (("body.xml", body), ("styles.xml", styles), ("settings.xml", settings)):
        open(os.path.join(d, f), "w").write(c)
    subprocess.run([sys.executable, MK, os.path.join(OUT, name + ".docx"), os.path.join(d, "body.xml"),
                    os.path.join(d, "styles.xml"), os.path.join(d, "settings.xml")], check=True)
print("ok")
