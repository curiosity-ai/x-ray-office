import subprocess,re,sys,math
def words(pdf):
    out=subprocess.run(['pdftotext','-bbox',pdf,'-'],capture_output=True,text=True).stdout
    page=0; res=[]
    for line in out.splitlines():
        if '<page ' in line: page+=1
        m=re.search(r'xMin="([\d.-]+)" yMin="([\d.-]+)" xMax="([\d.-]+)".*>(.*)</word>',line)
        if m: res.append((page,round(float(m.group(2))),float(m.group(1)),float(m.group(3)),m.group(4)))
    return res
ref=words('ref/fill2.xlsx/fill2.pdf')
units={73:'-',103:'ab',133:'W',163:'iii',193:'7',224:'x',252:'ab',291:'-',317:'Mw'}
# exact unit widths (pt) from ref glyph advances
cells=[]
rows={}
for r in ref: rows.setdefault((r[0],r[1]),[]).append(r)
starts={p:sorted(r[2] for r in rs) for (p,y),rs in rows.items() if y==73}
for (p,y),rs in rows.items():
    if y not in units: continue
    u=units[y]
    for r in rs:
        xs=starts[p]; i=min(range(len(xs)),key=lambda i:abs(xs[i]-r[2]))
        if abs(xs[i]-r[2])>0.3 or i+1>=len(xs): continue
        if len(r[4])%len(u) or r[4]!=u*(len(r[4])//len(u)): continue
        n=len(r[4])//len(u); w=(r[3]-r[2])/len(r[4])*len(u)
        colpt=xs[i+1]-xs[i]
        cells.append((p,y,colpt,w,n))
def test(name,f):
    bad=sum(1 for c in cells if f(*c[2:4])!=c[4])
    print(name,bad,'of',len(cells))
mm=lambda pt: pt*2540/72
test('exact pt', lambda col,w: math.floor((col-2)/w))
test('px720 round, avail floor', lambda col,w: math.floor((math.floor(col*10)-20)/round(w*10)))
test('px720 round, avail col twips', lambda col,w: (int(round(col*20)*0.5)-20)//round(w*10))
test('px720 trunc', lambda col,w: (int(round(col*20)*0.5)-20)//int(w*10))
test('px720 ceil', lambda col,w: (int(round(col*20)*0.5)-20)//math.ceil(w*10))
test('mm100 then px', lambda col,w: (int(round(col*20)*0.5)-20)//round(round(mm(w))*720/2540))
test('mm100 trunc then px', lambda col,w: (int(round(col*20)*0.5)-20)//round(int(mm(w))*720/2540))
test('mm100 avail', lambda col,w: (int(mm(col))-2*int(mm(1)))//round(mm(w)))
test('twips', lambda col,w: (round(col*20)-40)//round(w*20))
test('twips+1', lambda col,w: (round(col*20)-40)//(round(w*20)+1))
for c in cells[:5]: print(c)
tw2mm=lambda t: t*635//360
test('mm100 snap col, round w', lambda col,w: (tw2mm(round(col*20))-2*tw2mm(20))//round(mm(w)))
test('mm100 snap col, trunc w', lambda col,w: (tw2mm(round(col*20))-2*tw2mm(20))//int(mm(w)))
test('mm100 snap col, ceil w', lambda col,w: (tw2mm(round(col*20))-2*tw2mm(20))//math.ceil(mm(w)))
test('mm100 snap col, round w +1', lambda col,w: (tw2mm(round(col*20))-2*tw2mm(20))//(round(mm(w))+1))
test('mm100 snapcol-tw40', lambda col,w: tw2mm(round(col*20)-40)//round(mm(w)))
for c in cells:
    col,w,n=c[2],c[3],c[4]
    v=(tw2mm(round(col*20))-2*tw2mm(20))/mm(w)
    if (tw2mm(round(col*20))-2*tw2mm(20))//round(mm(w))!=n: print('miss',c, round(v,3), round(mm(w),2))
