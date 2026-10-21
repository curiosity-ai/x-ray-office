import sys, subprocess, re, os, glob, filecmp
R, B, N = sys.argv[1], sys.argv[2], sys.argv[3]
rx = re.compile(r'<page width="([\d.]+)" height="([\d.]+)">|<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">([^<]*)</word>')
def words(p):
    out = subprocess.run(['pdftotext','-bbox',p,'-'],capture_output=True,text=True).stdout
    pages=[]
    for m in rx.finditer(out):
        if m.group(1): pages.append([])
        else: pages[-1].append((float(m.group(3)),float(m.group(4)),float(m.group(5)),float(m.group(6)),m.group(7)))
    return pages
import difflib
def dist(a,b):
    pa,pb=words(a),words(b)
    tot=0;n=0;worst=0;unm=0
    for wa,wb in zip(pa,pb):
        sm=difflib.SequenceMatcher(None,[w[4] for w in wa],[w[4] for w in wb],autojunk=False)
        for t,i1,i2,j1,j2 in sm.get_opcodes():
            if t=='equal':
                for x,y in zip(wa[i1:i2],wb[j1:j2]):
                    d=max(abs(x[0]-y[0]),abs(x[1]-y[1]))
                    tot+=d;n+=1;worst=max(worst,d)
            else: unm+=max(i2-i1,j2-j1)
    return len(pa),len(pb),n,(tot/n if n else 0),worst,unm
for nf in sorted(glob.glob(N+'/*/*.pdf')):
    rel=os.path.relpath(nf,N); bf=os.path.join(B,rel); rf=os.path.join(R,rel)
    if os.path.exists(bf) and filecmp.cmp(bf,nf,shallow=False): continue
    if not os.path.exists(rf): print(rel,'no-ref'); continue
    b=dist(rf,bf); n=dist(rf,nf)
    print(f'{rel}\tpages {b[1]}->{n[1]} (ref {b[0]})\tmean {b[3]:.3f}->{n[3]:.3f}\tworst {b[4]:.2f}->{n[4]:.2f}\tunmatched {b[5]}->{n[5]}\tmatched {b[2]}/{n[2]}')
