#!/usr/bin/env python3
"""patch-sep.py in.doc out.doc sprm=value...: rewrite the operand of every occurrence of a section sprm in
WordDocument in place (0x5032 sprmSClm, two bytes; 0x7030 sprmSDxtCharSpace, four; 0x9031 sprmSDyaLinePitch,
two). Byte-pattern based: check the hits it prints. Run with python3 -I."""
import struct, sys, shutil, olefile
src, dst = sys.argv[1], sys.argv[2]
shutil.copy(src, dst)
o = olefile.OleFileIO(dst, write_mode=True)
w = bytearray(o.openstream("WordDocument").read())
for arg in sys.argv[3:]:
    k, v = arg.split("="); sprm = int(k, 16); v = int(v)
    size = 4 if sprm == 0x7030 else 2
    hits = [i for i in range(len(w) - 2 - size) if w[i] | (w[i + 1] << 8) == sprm]
    for i in hits:
        w[i + 2:i + 2 + size] = struct.pack("<i" if size == 4 else "<h", v)
    print(hex(sprm), "hits", hits)
o.write_stream("WordDocument", bytes(w)); o.close()
