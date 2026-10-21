import sys; sys.path.insert(0,'/tmp/claude-0/wordgaps/bin'); from mkdocx import *
cs = '<w:style w:type="character" w:styleId="Up"><w:name w:val="Up"/><w:rPr><w:position w:val="6"/></w:rPr></w:style>'
ps = '<w:style w:type="paragraph" w:styleId="PUp"><w:name w:val="PUp"/><w:basedOn w:val="Normal"/><w:rPr><w:position w:val="6"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="PDn"><w:name w:val="PDn"/><w:basedOn w:val="Normal"/><w:rPr><w:position w:val="-6"/></w:rPr></w:style>'
def doc(name, run, styles='', ppr=''):
    write(f'{name}.docx', p(r('Alpha')) + p(r('Base '), run, r(' tail'), ppr=ppr) + p(r('Charlie')), styles)
doc('none', r('MARK'))
for v in (6, -6, 12, -12, 3, 20):
    doc(f'pos{v}', r('MARK', f'<w:position w:val="{v}"/>'))
doc('pos6sz36', r('MARK', '<w:position w:val="6"/><w:sz w:val="36"/>'))
doc('pos6va', r('MARK', '<w:vertAlign w:val="superscript"/><w:position w:val="6"/>'))
doc('va', r('MARK', '<w:vertAlign w:val="superscript"/>'))
doc('cstyle', r('MARK', '<w:rStyle w:val="Up"/>'), cs)
doc('pstyleup', r('MARK'), ps, '<w:pStyle w:val="PUp"/>')
doc('pstyledn', r('MARK'), ps, '<w:pStyle w:val="PDn"/>')
