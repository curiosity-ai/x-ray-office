head = r"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}{\colortbl;\red255\green0\blue0;\red0\green0\blue255;\red255\green255\blue0;}"
st = r"{\stylesheet{\s0 Normal;}{\s1\brdrb\brdrs\brdrw30\brsp40 Bordered;}{\s2\sb240 Spaced;}}"
pg = r"\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
cases = {
 "style2": head+st+pg+r"\pard\fs24 Alpha before\par\pard\s1 Bravo styled\par\pard\s2 Spaced\par\pard Charlie after\par}",
 "spnoline": head+pg+r"\pard\fs24 Alpha before\par\pard\brdrb\brdrw30\brsp100 Bravo bottom\par\pard Charlie after\par}",
 "boxlr": head+pg+r"\pard\fs24 Alpha before\par\pard\li720\ri720\fi360\box\brdrs\brdrw20\brsp60 Bravo boxed with indents and a longer text that wraps onto a second line because it is long enough to need it\par\pard Charlie after\par}",
 "tabs": head+pg+r"\pard\fs24 Alpha before\par\pard\tx2880\brdrl\brdrs\brdrw20\brsp200 Bravo\tab tabbed\par\pard\tx2880 Bravo\tab tabbed\par}",
}
for k,v in cases.items(): open(f"{k}.rtf","w").write(v)
