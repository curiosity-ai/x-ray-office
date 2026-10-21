import sys, subprocess, re, glob, os
P='/home/user/x-ray-office/.claude/skills/render-comparison/scripts/pdf-ops.py'
def recs(pdf):
    out = subprocess.run(['python3',P,'dump',pdf,'--page','1','--only','text'],capture_output=True,text=True).stdout
    r=[]
    for line in out.splitlines():
        m=re.match(r'text\s+p\d+\s+\(\s*([-\d.]+),\s*([-\d.]+)\)\s+([\d.]+)pt(.*)', line)
        if m: r.append((float(m.group(1)),float(m.group(2)),float(m.group(3)),m.group(4)))
    return r
def key(pdf):
    rs=recs(pdf)
    if len(rs) < 3: return None
    mid=rs[1:-1]
    base=mid[0]; mark=mid[1] if len(mid)>=3 else mid[0]
    return (round(rs[0][1]-base[1],2), round(mark[1]-base[1],2), round(base[1]-rs[-1][1],2), mark[2])
D=sys.argv[1]; bad=0; n=0
for f in sorted(glob.glob(f'{D}/ref/*.pdf')):
    s=os.path.basename(f); o=f'{D}/ours/{s}'
    if not os.path.exists(o): print('MISSING',s); continue
    a=key(f); b=key(o); n+=1
    ok = a is not None and b is not None and all(abs(p-q)<=0.06 for p,q in zip(a[:3],b[:3]))
    if not ok: bad+=1
    if not ok or '-v' in sys.argv: print(('OK  ' if ok else 'DIFF'), s[:-4], 'ref', a, 'ours', b)
print(f'{n-bad} of {n} agree (pitch above, rise, pitch below within 0.06)')
