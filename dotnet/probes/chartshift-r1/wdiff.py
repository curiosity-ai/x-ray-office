import sys, subprocess, re
rx = re.compile(r'<page width="([\d.]+)" height="([\d.]+)">|<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">([^<]*)</word>')
def words(p):
    out = subprocess.run(['pdftotext','-bbox',p,'-'],capture_output=True,text=True).stdout
    pages=[]
    for m in rx.finditer(out):
        if m.group(1): pages.append([])
        else: pages[-1].append((float(m.group(3)),float(m.group(4)),float(m.group(5)),float(m.group(6)),m.group(7)))
    return pages
pa,pb=words(sys.argv[1]),words(sys.argv[2])
for i,(wa,wb) in enumerate(zip(pa,pb)):
    for x,y in zip(wa,wb):
        print(f'p{i+1} {x[4][:18]:18} x={x[0]:8.2f} y={x[1]:8.2f} w={x[2]-x[0]:7.2f}  dx={y[0]-x[0]:+6.2f} dy={y[1]-x[1]:+6.2f} dw={(y[2]-y[0])-(x[2]-x[0]):+6.2f} dh={(y[3]-y[1])-(x[3]-x[1]):+6.2f}')
