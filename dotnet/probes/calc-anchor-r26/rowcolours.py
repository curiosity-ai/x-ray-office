import sys, subprocess, tempfile, os, collections
from PIL import Image
pdf=sys.argv[1]; d=tempfile.mkdtemp()
subprocess.run(['pdftoppm','-r','72','-f','1','-l','1','-png',pdf,d+'/p'],check=True)
im=Image.open(d+'/'+os.listdir(d)[0]).convert('RGB'); w,h=im.size; px=im.load()
# darkest pixel per text row band: group consecutive rows having ink
rows=[]; cur=None
for y in range(h):
    ink=[px[x,y] for x in range(w) if sum(px[x,y])<600]
    if ink:
        if cur is None: cur=[y,y,collections.Counter()]
        cur[1]=y; cur[2].update(c for c in ink if sum(c)<300 or max(c)-min(c)>60)
    elif cur: rows.append(cur); cur=None
for y0,y1,c in rows:
    if y1-y0>8: print(y0,y1,['#%02x%02x%02x'%k for k,_ in c.most_common(2)])
