import sys, subprocess, tempfile, os
from PIL import Image
# usage: redbox.py pdf [dpi]  -> bbox of pure-ish red pixels per page, in px and pt
pdf=sys.argv[1]; dpi=int(sys.argv[2]) if len(sys.argv)>2 else 50
d=tempfile.mkdtemp()
subprocess.run(['pdftoppm','-r',str(dpi),'-png',pdf,d+'/p'],check=True)
for f in sorted(os.listdir(d)):
    im=Image.open(d+'/'+f).convert('RGB'); w,h=im.size; px=im.load()
    xs=[];ys=[]
    for y in range(h):
        for x in range(w):
            r,g,b=px[x,y]
            if r>200 and g<80 and b<80: xs.append(x); ys.append(y)
    if xs:
        x0,x1,y0,y1=min(xs),max(xs),min(ys),max(ys)
        k=72/dpi
        print(f, 'px %dx%d at (%d,%d)'%(x1-x0+1,y1-y0+1,x0,y0), 'pt %.1fx%.1f at (%.1f,%.1f)'%((x1-x0+1)*k,(y1-y0+1)*k,x0*k,y0*k))
    else: print(f,'none')
