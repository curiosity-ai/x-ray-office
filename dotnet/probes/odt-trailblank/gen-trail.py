#!/usr/bin/env python3
"""ODF trailing-blank decorations (tdf#168777): one .fodt per settings variant, an underlined and a
struck-through paragraph whose lines end in blanks. Writes u*.fodt into the directory given as argument."""
import os, sys
OUT = sys.argv[1]
WORDS = " ".join(f"word{i:02d}" for i in range(40))
def item(name, value):
    return f'<config:config-item config:name="{name}" config:type="boolean">{value}</config:config-item>'
def fodt(items):
    settings = ('<office:settings><config:config-item-set config:name="ooo:configuration-settings">'
                + "".join(item(n, v) for n, v in items) + '</config:config-item-set></office:settings>') if items else ''
    return f'''<?xml version="1.0" encoding="UTF-8"?>
<office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
 xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
 xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:config="urn:oasis:names:tc:opendocument:xmlns:config:1.0"
 office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
 {settings}
 <office:font-face-decls><style:font-face style:name="Liberation Serif" svg:font-family="'Liberation Serif'" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"/></office:font-face-decls>
 <office:styles><style:default-style style:family="paragraph"><style:text-properties style:font-name="Liberation Serif" fo:font-size="12pt"/></style:default-style></office:styles>
 <office:automatic-styles>
  <style:page-layout style:name="pm1"><style:page-layout-properties fo:page-width="8.5in" fo:page-height="11in" fo:margin-top="1in" fo:margin-bottom="1in" fo:margin-left="1in" fo:margin-right="3in"/></style:page-layout>
  <style:style style:name="T1" style:family="text"><style:text-properties style:text-underline-style="solid" style:text-underline-width="auto" style:text-underline-color="font-color"/></style:style>
  <style:style style:name="T2" style:family="text"><style:text-properties style:text-line-through-style="solid"/></style:style>
 </office:automatic-styles>
 <office:master-styles><style:master-page style:name="Standard" style:page-layout-name="pm1"/></office:master-styles>
 <office:body><office:text>
  <text:p><text:span text:style-name="T1">U {WORDS}</text:span></text:p>
  <text:p><text:span text:style-name="T2">S {WORDS}</text:span></text:p>
 </office:text></office:body></office:document>'''
CASES = {
    "u0-none": [],
    "u1-comp": [("MsWordCompTrailingBlanks", "true")],
    "u2-comp-ul": [("MsWordCompTrailingBlanks", "true"), ("MsWordUlTrailSpace", "true")],
    "u3-ul": [("MsWordUlTrailSpace", "true")],
    "u4-comp-false": [("MsWordCompTrailingBlanks", "false")],
}
for name, items in CASES.items():
    open(os.path.join(OUT, name + ".fodt"), "w").write(fodt(items))
print("ok")
