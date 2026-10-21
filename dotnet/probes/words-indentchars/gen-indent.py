#!/usr/bin/env python3
"""Character-unit indents and line-unit spacing: one DOCX per case, so each paragraph is measured alone.

Writes <case>.docx into the directory given as the first argument (default: beside this script)."""
import os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else HERE
MK = os.path.join(HERE, "mkdocx.py")
WORDS = " ".join(f"w{i:02d}" for i in range(60))

def para(ind="", spacing='w:before="0" w:after="0"', ppr="", rpr="", text=WORDS, tag="X"):
    ind_el = f'<w:ind {ind}/>' if ind else ''
    return (f'<w:p><w:pPr>{ppr}<w:spacing {spacing}/>{ind_el}<w:rPr>{rpr}</w:rPr></w:pPr>'
            f'<w:r><w:rPr>{rpr}</w:rPr><w:t xml:space="preserve">{tag} {text}</w:t></w:r></w:p>')

STYLE_IND = ('<w:style w:type="paragraph" w:styleId="Ch"><w:name w:val="Ch"/><w:basedOn w:val="Normal"/>'
             '<w:pPr><w:ind w:leftChars="300" w:left="200"/></w:pPr></w:style>'
             '<w:style w:type="paragraph" w:styleId="Big"><w:name w:val="Big"/><w:basedOn w:val="Normal"/>'
             '<w:rPr><w:sz w:val="40"/></w:rPr></w:style>'
             '<w:style w:type="paragraph" w:styleId="ChZero"><w:name w:val="ChZero"/><w:basedOn w:val="Normal"/>'
             '<w:pPr><w:ind w:leftChars="0" w:left="1440"/></w:pPr></w:style>')

def table(style, rows=2):
    cell = lambda t, first: (f'<w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/></w:tcPr>'
                             f'<w:p><w:pPr><w:jc w:val="right"/></w:pPr><w:r><w:t>{t}</w:t></w:r></w:p></w:tc>')
    trs = "".join(f'<w:tr>{cell("First" + str(r), True)}{cell("Second" + str(r), False)}</w:tr>' for r in range(rows))
    return (f'<w:tbl><w:tblPr><w:tblStyle w:val="{style}"/><w:tblW w:w="6000" w:type="dxa"/>'
            '<w:tblLook w:val="04A0" w:firstRow="0" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>'
            '</w:tblPr><w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="3000"/></w:tblGrid>' + trs + '</w:tbl><w:p/>')

def tstyle(sid, ppr="", first=""):
    fc = f'<w:tblStylePr w:type="firstCol"><w:pPr>{first}</w:pPr></w:tblStylePr>' if first else ''
    return (f'<w:style w:type="table" w:styleId="{sid}"><w:name w:val="{sid}"/><w:pPr>{ppr}</w:pPr>'
            f'<w:tblPr><w:tblCellMar><w:left w:w="0" w:type="dxa"/><w:right w:w="0" w:type="dxa"/></w:tblCellMar></w:tblPr>{fc}</w:style>')

STYLE_IND += ('<w:style w:type="paragraph" w:styleId="RZero"><w:name w:val="RZero"/><w:basedOn w:val="Normal"/>'
              '<w:pPr><w:ind w:rightChars="0" w:right="374"/></w:pPr></w:style>'
              + tstyle("TPlain", '<w:ind w:right="374"/>')
              + tstyle("TZero", '<w:ind w:rightChars="0" w:right="374"/>')
              + tstyle("TFirst", '<w:ind w:left="374"/>', '<w:ind w:leftChars="0" w:left="0" w:rightChars="0" w:right="374"/>')
              + tstyle("TFirstPlain", '<w:ind w:left="374"/>', '<w:ind w:left="0" w:right="374"/>')
              + tstyle("TChars", '<w:ind w:rightChars="200" w:right="374"/>'))

CASES = {
    "t00-plain": table("TPlain"),
    "t01-rightChars0": table("TZero"),
    "t02-firstCol-rightChars0": table("TFirst"),
    "t03-firstCol-plain": table("TFirstPlain"),
    "t04-rightChars200": table("TChars"),
    "i18-styleRZero": para(ppr='<w:pStyle w:val="RZero"/>'),
    "i00-plain": para(),
    "i01-right374": para('w:right="374"'),
    "i02-rightChars0-right374": para('w:rightChars="0" w:right="374"'),
    "i03-right374-rightChars0": para('w:right="374" w:rightChars="0"'),
    "i04-leftChars0-left720": para('w:leftChars="0" w:left="720"'),
    "i05-leftChars200": para('w:leftChars="200"'),
    "i06-leftChars200-left720": para('w:leftChars="200" w:left="720"'),
    "i07-rightChars200": para('w:rightChars="200"'),
    "i08-firstLineChars200": para('w:firstLineChars="200"'),
    "i09-hangingChars100-left720": para('w:hangingChars="100" w:left="720"'),
    "i10-leftChars200-sz32run": para('w:leftChars="200"', rpr='<w:sz w:val="32"/>'),
    "i11-leftChars200-styleBig": para('w:leftChars="200"', ppr='<w:pStyle w:val="Big"/>'),
    "i12-styleCh": para(ppr='<w:pStyle w:val="Ch"/>'),
    "i13-styleCh-left720": para('w:left="720"', ppr='<w:pStyle w:val="Ch"/>'),
    "i14-styleCh-leftChars0": para('w:leftChars="0"', ppr='<w:pStyle w:val="Ch"/>'),
    "i15-styleChZero": para(ppr='<w:pStyle w:val="ChZero"/>'),
    "i16-leftChars200-eastAsiaSz": para('w:leftChars="200"', rpr='<w:sz w:val="21"/><w:szCs w:val="40"/>'),
    "i17-firstLineChars0-firstLine720": para('w:firstLineChars="0" w:firstLine="720"'),
    "s00-plain": para(tag="A", text="one") + para(tag="B", text="two"),
    "s01-beforeLines100": para(tag="A", text="one") + para(tag="B", text="two", spacing='w:beforeLines="100" w:after="0"'),
    "s02-before0-beforeLines100": para(tag="A", text="one") + para(tag="B", text="two", spacing='w:before="0" w:beforeLines="100" w:after="0"'),
    "s03-beforeLines100-before0": para(tag="A", text="one") + para(tag="B", text="two", spacing='w:beforeLines="100" w:before="0" w:after="0"'),
    "s04-afterLines50": para(tag="A", text="one", spacing='w:before="0" w:afterLines="50"') + para(tag="B", text="two"),
    "s05-beforeLines33": para(tag="A", text="one") + para(tag="B", text="two", spacing='w:beforeLines="33" w:after="0"'),
}
STYLES = ('<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif" '
          'w:eastAsia="WenQuanYi Zen Hei"/><w:sz w:val="21"/></w:rPr></w:rPrDefault>'
          '<w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>'
          '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>' + STYLE_IND)
for name, body in CASES.items():
    d = os.path.join(OUT, name + ".src"); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, "body.xml"), "w").write(body)
    open(os.path.join(d, "styles.xml"), "w").write(STYLES)
    open(os.path.join(d, "settings.xml"), "w").write('')
    subprocess.run([sys.executable, MK, os.path.join(OUT, name + ".docx"), os.path.join(d, "body.xml"),
                    os.path.join(d, "styles.xml"), os.path.join(d, "settings.xml")], check=True)
print("ok")
