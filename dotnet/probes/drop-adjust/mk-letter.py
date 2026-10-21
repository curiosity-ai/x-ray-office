import sys; sys.path.insert(0,'/tmp/claude-0/wordgaps/bin'); from mkdocx import *
L="Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur."
def doc(name, jc, lines=3, text=L):
    fp=f'<w:framePr w:dropCap="drop" w:lines="{lines}" w:wrap="around" w:vAnchor="text" w:hAnchor="text"/>'
    body=p(r('BEFORE text'))+p(r('W'), ppr=fp+(f'<w:jc w:val="{jc}"/>' if jc else ''))+p(r('AFTER '+text), ppr=(f'<w:jc w:val="{jc}"/>' if jc else ''))
    write(f'{name}.docx', body)
doc('left', None); doc('center','center'); doc('right','right'); doc('center2','center',2)
doc('centershort','center',3,'AFTER short line one. And more text to wrap onto a few lines so we see.')
