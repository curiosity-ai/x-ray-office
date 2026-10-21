import sys, subprocess, re
P='/home/user/x-ray-office/.claude/skills/render-comparison/scripts/pdf-ops.py'
def geo(pdf, page=1):
    out = subprocess.run(['python3',P,'dump',pdf,'--page',str(page)],capture_output=True,text=True).stdout
    items=[]
    for line in out.splitlines():
        if line.startswith('#') or not line.strip(): continue
        nums = re.findall(r'-?\d+\.\d+', line)
        kind=line.split()[0]
        if kind=='text':
            name=re.search(r'"(.*)"',line); items.append(f"T[{(name.group(1) if name else '?')[:8]}]@{nums[0]},{nums[1]}")
        else:
            col=re.search(r'#[0-9A-F]{6}',line)
            items.append(f"{kind[0]}({nums[0]},{nums[1]})-({nums[2]},{nums[3]}){col.group(0) if col else ''}")
    return items
for s in sys.argv[2:]:
    print('==',s)
    for w in ('ref','ours'):
        print('  %-4s'%w, '  '.join(geo(f'{sys.argv[1]}/{w}/{s}.pdf')))
