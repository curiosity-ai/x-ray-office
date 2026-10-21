import sys
head = r"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}{\colortbl;\red255\green0\blue0;\red0\green0\blue255;\red255\green255\blue0;}\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
cases = {
 "none": r"\pard\fs24 Alpha before\par\pard Bravo plain\par\pard Charlie after\par",
 "top": r"\pard\fs24 Alpha before\par\pard\brdrt\brdrs\brdrw10 Bravo top\par\pard Charlie after\par",
 "topsp": r"\pard\fs24 Alpha before\par\pard\brdrt\brdrs\brdrw10\brsp80 Bravo top\par\pard Charlie after\par",
 "bot": r"\pard\fs24 Alpha before\par\pard\brdrb\brdrs\brdrw30\brsp40\brdrcf1 Bravo bottom\par\pard Charlie after\par",
 "botdb": r"\pard\fs24 Alpha before\par\pard\brdrb\brdrdb\brdrw15 Bravo bottom\par\pard Charlie after\par",
 "nostyle": r"\pard\fs24 Alpha before\par\pard\brdrb\brdrw30 Bravo bottom\par\pard Charlie after\par",
 "nowidth": r"\pard\fs24 Alpha before\par\pard\brdrb\brdrs Bravo bottom\par\pard Charlie after\par",
 "box": r"\pard\fs24 Alpha before\par\pard\box\brdrs\brdrw20\brsp60\brdrcf2 Bravo boxed\par\pard Charlie after\par",
 "box2": r"\pard\fs24 Alpha before\par\pard\box\brdrs\brdrw20\brsp60 Bravo boxed\par\pard\box\brdrs\brdrw20\brsp60 Delta boxed\par\pard Charlie after\par",
 "btw": r"\pard\fs24 Alpha before\par\pard\box\brdrs\brdrw20\brsp60\brdrbtw\brdrs\brdrw10 Bravo boxed\par\pard\box\brdrs\brdrw20\brsp60\brdrbtw\brdrs\brdrw10 Delta boxed\par\pard Charlie after\par",
 "sides": r"\pard\fs24 Alpha before\par\pard\brdrl\brdrs\brdrw30\brsp100\brdrr\brdrs\brdrw30\brsp100 Bravo sides\par\pard Charlie after\par",
 "shade": r"\pard\fs24 Alpha before\par\pard\cbpat3 Bravo shaded\par\pard Charlie after\par",
 "shadebox": r"\pard\fs24 Alpha before\par\pard\cbpat3\box\brdrs\brdrw20\brsp60 Bravo shaded\par\pard Charlie after\par",
 "style": r"{\stylesheet{\s0 Normal;}{\s1\brdrb\brdrs\brdrw30\brsp40 Bordered;}}\pard\fs24 Alpha before\par\pard\s1 Bravo styled\par\pard Charlie after\par",
}
for k,v in cases.items():
    open(f"{k}.rtf","w").write(head+v+"}")
