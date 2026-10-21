#!/usr/bin/env python3
"""Character-grid probe: one DOCX per w:docGrid variant, the same paragraphs in each.

Writes <variant>.docx beside this script, or into the directory given as the first argument."""
import os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else HERE
MK = os.path.join(HERE, "mkdocx.py")
CJK = "永和九年岁在癸丑暮春之初会于会稽山阴之兰亭修禊事也群贤毕至少长咸集此地有崇山峻岭茂林修竹又有清流激湍映带左右引以为流觞曲水列坐其次"
PUNCT = "「永和」九年、岁在癸丑。暮春之初，会于会稽山阴之兰亭。"

def para(text, extra="", rpr=""):
    run = f'<w:r><w:rPr>{rpr}</w:rPr><w:t xml:space="preserve">{text}</w:t></w:r>' if text else ''
    return f'<w:p><w:pPr>{extra}<w:spacing w:before="0" w:after="0"/></w:pPr>{run}</w:p>'

def body(grid, sz):
    ps = [
        para("P1" + CJK),
        para("P2 Latin words only here to see whether a Latin portion moves under the grid at all ok"),
        para("P3" + PUNCT),
        para("P4 mixed 中文 text 汉字 and more 文字"),
        para("P5" + CJK[:20], rpr='<w:sz w:val="32"/>'),
        para("P6" + CJK, extra='<w:jc w:val="both"/>'),
        para("P7" + CJK[:20], extra='<w:snapToGrid w:val="0"/>'),
        para("P8 short words then a long one " + "Pneumonoultramicroscopicsilicovolcanoconiosis" * 2 + " end",
             extra='<w:jc w:val="both"/>'),
        para("P9 汉字 and blanks 文字 justified 中文 with enough of both to wrap onto a second line of the "
             "paragraph 汉字文字 here", extra='<w:jc w:val="both"/>'),
    ]
    sect = ('<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" '
            f'w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>{grid}</w:sectPr>')
    return "".join(ps) + sect

def styles(sz, normal_sz=None):
    nsz = f'<w:rPr><w:sz w:val="{normal_sz}"/><w:szCs w:val="{normal_sz}"/></w:rPr>' if normal_sz else ''
    return ('<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Liberation Serif" w:hAnsi="Liberation Serif" '
            f'w:eastAsia="WenQuanYi Zen Hei"/><w:sz w:val="{sz}"/><w:szCs w:val="{sz}"/></w:rPr></w:rPrDefault>'
            '<w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>'
            f'<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/>{nsz}</w:style>')

G = lambda t, cs: f'<w:docGrid w:type="{t}" w:linePitch="312"' + (f' w:charSpace="{cs}"' if cs is not None else '') + '/>'
VARIANTS = {
    "c0-lines": (G("lines", None), 21, None),
    "c1-lac-cs0": (G("linesAndChars", 0), 21, None),
    "c2-lac-neg": (G("linesAndChars", -3426), 21, None),
    "c3-lac-pos": (G("linesAndChars", 4096), 21, None),
    "c4-stc-pos": (G("snapToChars", 4096), 21, None),
    "c5-stc-neg": (G("snapToChars", -3426), 21, None),
    "c6-lac-big": (G("linesAndChars", 40960), 21, None),
    "c7-lac-normal24": (G("linesAndChars", 4096), 21, 24),
    "c8-stc-cs0": (G("snapToChars", 0), 21, None),
    "c9-lac-frac": (G("linesAndChars", 2048), 21, None),
}
for name, (grid, sz, nsz) in VARIANTS.items():
    d = os.path.join(OUT, name + ".src"); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, "body.xml"), "w").write(body(grid, sz))
    open(os.path.join(d, "styles.xml"), "w").write(styles(sz, nsz))
    open(os.path.join(d, "settings.xml"), "w").write('')
    subprocess.run([sys.executable, MK, os.path.join(OUT, name + ".docx"), os.path.join(d, "body.xml"),
                    os.path.join(d, "styles.xml"), os.path.join(d, "settings.xml")], check=True)
print("ok")
