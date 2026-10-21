import sys,subprocess,re
def words(pdf):
    out=subprocess.run(['pdftotext','-bbox',pdf,'-'],capture_output=True,text=True).stdout
    page=0; res=[]
    for line in out.splitlines():
        if '<page ' in line: page+=1
        m=re.search(r'xMin="([\d.-]+)" yMin="([\d.-]+)".*>(.*)</word>',line)
        if m: res.append((page,round(float(m.group(2))),float(m.group(1)),m.group(3)))
    return res
a=words(sys.argv[1]); b=words(sys.argv[2])
# key by page,y-row rank, x rounded
def key(ws):
    d={}
    for p,y,x,t in ws: d.setdefault((p,y),[]).append((x,t))
    return [ (k,sorted(v)) for k,v in sorted(d.items())]
ka=key(a); kb=key(b); bad=0; tot=0
for (k1,v1),(k2,v2) in zip(ka,kb):
    for (x1,t1),(x2,t2) in zip(v1,v2):
        tot+=1
        if t1!=t2: bad+=1; print(k1, round(x1), len(t1), len(t2), t1[:4])
print('mismatch',bad,'of',tot, len(ka),len(kb))
