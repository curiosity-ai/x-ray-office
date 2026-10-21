import sys, subprocess, re, os, glob
A, B = sys.argv[1], sys.argv[2]
rx = re.compile(r'<page width="([\d.]+)" height="([\d.]+)">|<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">([^<]*)</word>')
def words(p):
    out = subprocess.run(['pdftotext','-bbox',p,'-'],capture_output=True,text=True).stdout
    pages=[]; 
    for m in rx.finditer(out):
        if m.group(1): pages.append([])
        else: pages[-1].append((float(m.group(3)),float(m.group(4)),float(m.group(5)),float(m.group(6)),m.group(7)))
    return pages
rows=[]
for a in sorted(glob.glob(A+'/*/*.pdf')):
    rel=os.path.relpath(a,A); b=os.path.join(B,rel)
    if not os.path.exists(b): rows.append((rel,'missing-b',0,'')); continue
    pa,pb=words(a),words(b)
    if len(pa)!=len(pb): rows.append((rel,f'pages {len(pa)}/{len(pb)}',999,'')); continue
    worst=0; where=''; note=''
    for i,(wa,wb) in enumerate(zip(pa,pb)):
        if [w[4] for w in wa]!=[w[4] for w in wb]:
            note='text-differs'; 
            continue
        for x,y in zip(wa,wb):
            d=max(abs(x[0]-y[0]),abs(x[1]-y[1]),abs(x[2]-y[2]),abs(x[3]-y[3]))
            if d>worst: worst=d; where=f'p{i+1} {x[4]!r} dx={y[0]-x[0]:+.2f} dy={y[1]-y[1]+ (y[1]-x[1]):+.2f} dw={(y[2]-y[0])-(x[2]-x[0]):+.2f}'
    if worst>0.01 or note: rows.append((rel,note or 'moved',worst,where))
for r in sorted(rows,key=lambda r:-r[2]): print(f'{r[0]}\t{r[1]}\t{r[2]:.2f}\t{r[3]}')
print('movers',len(rows),file=sys.stderr)
