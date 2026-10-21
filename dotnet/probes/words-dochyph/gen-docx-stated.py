#!/usr/bin/env python3
"""One DOCX with no settings and one paragraph stating <w:suppressAutoHyphens w:val="0"/>, written to the
path given as the argument. 26.8's daily hyphenates it (two lines end in a hyphen)."""
import os, subprocess, sys, tempfile
MK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mkdocx.py")
T = ("Internationalisation of extraordinarily complicated administrative responsibilities characteristically "
     "necessitates uncharacteristically comprehensive documentation and interdisciplinary collaboration "
     "between representatives.")
body = (f'<w:p><w:pPr><w:suppressAutoHyphens w:val="0"/></w:pPr><w:r><w:t>{T}</w:t></w:r></w:p>'
        '<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="5040" w:bottom="1440" '
        'w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>')
styles = ('<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/>'
          '<w:sz w:val="22"/><w:lang w:val="en-US"/></w:rPr></w:rPrDefault></w:docDefaults>'
          '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>')
with tempfile.TemporaryDirectory() as d:
    for n, c in (("b.xml", body), ("s.xml", styles), ("t.xml", "")):
        open(os.path.join(d, n), "w").write(c)
    subprocess.run([sys.executable, MK, sys.argv[1]] + [os.path.join(d, n) for n in ("b.xml", "s.xml", "t.xml")], check=True)
