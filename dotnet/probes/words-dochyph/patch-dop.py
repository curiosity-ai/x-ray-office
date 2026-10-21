#!/usr/bin/env python3
"""patch-dop.py in.doc out.doc field=value...: rewrite Dop fields in place (fAutoHyphen, fHyphCapitals,
dxaHotZ, cConsecHypLim, dropStyleNoAutoHyph=1), locating the Dop through the FIB's fcDop/lcbDop. Run with python3 -I."""
import struct, sys, olefile, shutil
src, dst = sys.argv[1], sys.argv[2]
shutil.copy(src, dst)
o = olefile.OleFileIO(dst, write_mode=True)
w = o.openstream("WordDocument").read()
table = "1Table" if struct.unpack_from("<H", w, 0xA)[0] & 0x200 else "0Table"
t = bytearray(o.openstream(table).read())
csw = struct.unpack_from("<H", w, 0x20)[0]; off = 0x22 + csw * 2
cslw = struct.unpack_from("<H", w, off)[0]; off += 2 + cslw * 4 + 2
fc, lcb = struct.unpack_from("<II", w, off + 31 * 8)
for arg in sys.argv[3:]:
    k, v = arg.split("="); v = int(v)
    if k == "fAutoHyphen": t[fc + 5] = (t[fc + 5] & ~0x10) | (0x10 if v else 0)
    elif k == "fHyphCapitals": t[fc + 5] = (t[fc + 5] & ~0x08) | (0x08 if v else 0)
    elif k == "dxaHotZ": struct.pack_into("<H", t, fc + 0x0E, v)
    elif k == "cConsecHypLim": struct.pack_into("<H", t, fc + 0x10, v)
    elif k == "dropStyleNoAutoHyph":
        # Rename every sprmPFNoAutoHyph (0x242A) in the stylesheet to sprmPFKinsoku (0x2433), a one-byte
        # paragraph sprm that cannot move Latin text, so the styles state no hyphenation of their own.
        sfc, slcb = struct.unpack_from("<II", w, off + 1 * 8)
        for i in range(sfc, sfc + slcb - 2):
            if t[i] == 0x2A and t[i + 1] == 0x24: t[i] = 0x33
    else: raise SystemExit("unknown field " + k)
o.write_stream(table, bytes(t)); o.close()
