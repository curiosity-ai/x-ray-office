#!/usr/bin/env python3
"""Empty paragraphs in a face declaring a CJK code page: does 26.8.1.1 still give them Word's 127 %?"""
import os, subprocess
HERE = os.path.dirname(os.path.abspath(__file__))
def p(tag, n, font="WenQuanYi Zen Hei", size=24):
    rpr = f'<w:rFonts w:ascii="{font}" w:hAnsi="{font}" w:eastAsia="{font}"/><w:sz w:val="{size}"/>'
    t = f'<w:r><w:rPr>{rpr}</w:rPr><w:t>{" ".join(f"{tag}w{i:02d}" for i in range(n))}</w:t></w:r>' if n else ''
    return f'<w:p><w:pPr><w:spacing w:before="0" w:after="0"/><w:rPr>{rpr}</w:rPr></w:pPr>{t}</w:p>'
body = "".join([p("A", 1), p("B", 0), p("C", 1), p("D", 0), p("E", 0), p("F", 1), p("G", 0, size=40), p("H", 1),
                p("I", 1, font="Liberation Serif"), p("J", 0, font="Liberation Serif"), p("K", 1, font="Liberation Serif")])
os.makedirs(os.path.join(HERE, "empty"), exist_ok=True)
open(os.path.join(HERE, "empty", "body.xml"), "w").write(body)
open(os.path.join(HERE, "empty", "styles.xml"), "w").write(
    '<w:docDefaults><w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>')
subprocess.run([os.path.join(HERE, "mkdocx.py"), os.path.join(HERE, "empty-cjk.docx"),
                os.path.join(HERE, "empty", "body.xml"), os.path.join(HERE, "empty", "styles.xml"), "-"], check=True)
