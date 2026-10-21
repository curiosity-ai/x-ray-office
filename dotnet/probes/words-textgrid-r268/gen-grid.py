#!/usr/bin/env python3
"""Text-grid probe: one DOCX per grid variant, the same paragraphs in each."""
import os, subprocess, sys
OUT = os.path.dirname(os.path.abspath(__file__))  # writes the .docx beside this script
MK = os.path.join(OUT, "mkdocx.py")

def words(tag, n):
    return " ".join(f"{tag}w{i:02d}" for i in range(n))

def para(tag, n, size=22, spacing="", font="Liberation Serif", extra=""):
    rpr = f'<w:rFonts w:ascii="{font}" w:hAnsi="{font}" w:eastAsia="{font}"/><w:sz w:val="{size}"/>'
    sp = f'<w:spacing w:before="0" w:after="0" {spacing}/>' if spacing else '<w:spacing w:before="0" w:after="0"/>'
    text = f'<w:r><w:rPr>{rpr}</w:rPr><w:t>{words(tag, n)}</w:t></w:r>' if n else ''
    return f'<w:p><w:pPr>{extra}{sp}<w:rPr>{rpr}</w:rPr></w:pPr>{text}</w:p>'

def body(grid):
    ps = [
        para("A", 30),
        para("B", 14, size=48),
        para("C", 30, spacing='w:line="360" w:lineRule="auto"'),
        para("D", 30, spacing='w:line="0" w:lineRule="atLeast"'),
        para("E", 30, spacing='w:line="500" w:lineRule="atLeast"'),
        para("F", 30, spacing='w:line="300" w:lineRule="exact"'),
        para("G", 1),
        para("H", 0),
        para("I", 1),
        para("J", 0, size=60),
        para("K", 1),
        para("L", 30, extra='<w:snapToGrid w:val="0"/>'),
        para("M", 12, size=24, font="WenQuanYi Zen Hei"),
        para("N", 30, spacing='w:line="180" w:lineRule="auto"'),
    ]
    cell = para("T", 30, spacing='w:line="360" w:lineRule="auto"') + para("U", 12, size=24, font="WenQuanYi Zen Hei")
    tbl = ('<w:tbl><w:tblPr><w:tblW w:w="6000" w:type="dxa"/></w:tblPr><w:tblGrid><w:gridCol w:w="6000"/></w:tblGrid>'
           f'<w:tr><w:tc><w:tcPr><w:tcW w:w="6000" w:type="dxa"/></w:tcPr>{cell}</w:tc></w:tr></w:tbl>')
    sect = ('<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" '
            f'w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>{grid}</w:sectPr>')
    return "".join(ps) + tbl + para("Z", 1) + sect

STYLES = ('<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif"/>'
          '<w:sz w:val="22"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" '
          'w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>'
          '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>')

VARIANTS = {
    "g0-none": ('<w:docGrid w:linePitch="360"/>', ''),
    "g1-lines360": ('<w:docGrid w:type="lines" w:linePitch="360"/>', ''),
    "g2-lac312": ('<w:docGrid w:type="linesAndChars" w:linePitch="312"/>', ''),
    "g3-lines360-adjust": ('<w:docGrid w:type="lines" w:linePitch="360"/>', '<w:compat><w:adjustLineHeightInTable/></w:compat>'),
    "g4-lines200": ('<w:docGrid w:type="lines" w:linePitch="200"/>', ''),
}
for name, (grid, settings) in VARIANTS.items():
    d = os.path.join(OUT, name); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, "body.xml"), "w").write(body(grid))
    open(os.path.join(d, "styles.xml"), "w").write(STYLES)
    open(os.path.join(d, "settings.xml"), "w").write(settings)
    subprocess.run([MK, os.path.join(OUT, name + ".docx"), os.path.join(d, "body.xml"),
                    os.path.join(d, "styles.xml"), os.path.join(d, "settings.xml")], check=True)
print("ok")
