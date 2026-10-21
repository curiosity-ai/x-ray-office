head = r"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}{\colortbl;\red255\green0\blue0;}\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
cases = {
 "dist1": r"\pard\brdrt\brdrs\brdrw10\brdrb\brdrw30\brsp100 Bravo\par",
 "dist2": r"\pard\brdrl\brdrs\brdrw10\brdrb\brdrw30\brsp100 Bravo\par",
 "dist3": r"\pard\brdrb\brdrnone\brsp100 Bravo\par",
}
for k,v in cases.items(): open(f"{k}.rtf","w").write(head+r"\pard\fs24 Alpha before\par"+v+r"\pard Charlie after\par}")
