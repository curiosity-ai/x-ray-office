# escapement matrix: ODT (fodt), RTF; faces x sizes x escapements
faces = {'ls':'Liberation Serif','lsa':'Liberation Sans','car':'Carlito','dvs':'DejaVu Serif'}
sizes = [8, 12, 20]
odf_pos = {'sup':'super 58%','sub':'sub 58%','p33':'33% 58%','p50':'50% 100%','m40':'-40% 70%','p100':'100% 40%','sup100':'super 100%','m20':'-20% 100%'}
def fodt(face, size, pos):
    return f'''<?xml version="1.0" encoding="UTF-8"?>
<office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
<office:font-face-decls><style:font-face style:name="F" svg:font-family="'{face}'" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"/></office:font-face-decls>
<office:styles><style:default-style style:family="paragraph"><style:paragraph-properties fo:margin-top="0in" fo:margin-bottom="0in"/><style:text-properties style:font-name="F" fo:font-size="{size}pt"/></style:default-style></office:styles>
<office:automatic-styles><style:style style:name="T1" style:family="text"><style:text-properties style:text-position="{pos}"/></style:style>
<style:page-layout style:name="pm1"><style:page-layout-properties fo:page-width="8.5in" fo:page-height="11in" fo:margin-top="1in" fo:margin-bottom="1in" fo:margin-left="1in" fo:margin-right="1in"/></style:page-layout></office:automatic-styles>
<office:master-styles><style:master-page style:name="Standard" style:page-layout-name="pm1"/></office:master-styles>
<office:body><office:text><text:p>Alpha</text:p><text:p>Base <text:span text:style-name="T1">MARK</text:span> tail</text:p><text:p>Charlie</text:p></office:text></office:body></office:document>'''
rtf_pos = {'sup':r'\super','sub':r'\sub','up6':r'\up6','dn6':r'\dn6','up':r'\up','dn12':r'\dn12','supup':r'\super\up8'}
def rtf(face, size, word):
    return (r"{\rtf1\ansi\deff0{\fonttbl{\f0 " + face + r";}}\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
            + rf"\pard\plain\fs{size*2} Alpha\par\pard\plain\fs{size*2} Base {{{word} MARK}} tail\par\pard\plain\fs{size*2} Charlie\par}}")
for fk, f in faces.items():
    for sz in sizes:
        for pk, pv in odf_pos.items():
            open(f'o-{fk}-{sz}-{pk}.fodt','w').write(fodt(f, sz, pv))
        for rk, rv in rtf_pos.items():
            open(f'r-{fk}-{sz}-{rk}.rtf','w').write(rtf(f, sz, rv))
