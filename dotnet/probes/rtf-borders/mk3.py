head = r"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}{\colortbl;\red255\green0\blue0;}\paperw12240\paperh15840\margl1440\margr1440\margt1440\margb1440 "
def P(t, box, btw): return r"\pard"+box+btw+" "+t+r"\par"
B=lambda sp: r"\box\brdrs\brdrw20\brsp%d"%sp
W=lambda sp: r"\brdrbtw\brdrs\brdrw10\brsp%d"%sp
cases = {
 "btw2": P("Bravo",B(60),W(100))+P("Delta",B(60),W(100)),
 "btw3": P("Bravo",B(20),W(0))+P("Delta",B(100),W(0)),
 "btw4": P("Bravo",B(100),W(0))+P("Delta",B(20),W(0)),
 "btw5": P("Bravo",B(60),"")+P("Delta",B(60),W(0)),
 "btw6": P("Bravo",B(60),W(0))+P("Delta",B(60),""),
}
for k,v in cases.items(): open(f"{k}.rtf","w").write(head+r"\pard\fs24 Alpha before\par"+v+r"\pard Charlie after\par}")
