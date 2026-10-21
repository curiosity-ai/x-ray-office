#!/usr/bin/env python3
"""mkdocx.py out.docx body.xml [styles.xml] [settings.xml] [numbering.xml]: minimal DOCX from XML fragments (files)."""
import sys, zipfile
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
out=sys.argv[1]; body=open(sys.argv[2]).read()
styles=open(sys.argv[3]).read() if len(sys.argv)>3 and sys.argv[3]!='-' else ''
settings=open(sys.argv[4]).read() if len(sys.argv)>4 and sys.argv[4]!='-' else ''
numbering=open(sys.argv[5]).read() if len(sys.argv)>5 else None
ct='''<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>'''+('<Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>' if numbering else '')+'</Types>'
rels='<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="word/document.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"/></Relationships>'
drels='<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="styles.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"/><Relationship Id="rId2" Target="settings.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings"/>'+('<Relationship Id="rId3" Target="numbering.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering"/>' if numbering else '')+'</Relationships>'
sect='<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>'
if '<w:sectPr' not in body.split('</w:p>')[-1]: body=body+sect
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml',ct); z.writestr('_rels/.rels',rels); z.writestr('word/_rels/document.xml.rels',drels)
    z.writestr('word/document.xml',f'<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="{W}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>{body}</w:body></w:document>')
    z.writestr('word/styles.xml',f'<?xml version="1.0" encoding="UTF-8"?><w:styles xmlns:w="{W}">{styles}</w:styles>')
    z.writestr('word/settings.xml',f'<?xml version="1.0" encoding="UTF-8"?><w:settings xmlns:w="{W}">{settings}</w:settings>')
    if numbering: z.writestr('word/numbering.xml',f'<?xml version="1.0" encoding="UTF-8"?><w:numbering xmlns:w="{W}">{numbering}</w:numbering>')
