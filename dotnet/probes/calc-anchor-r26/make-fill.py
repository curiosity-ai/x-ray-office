"""Fill-alignment probes for the repeat count (output2.cxx:1895-1914 in 26.8.1.1).

python3 -I make-fill.py <outdir> writes fill2.xlsx (the 151-cell fit: nine texts, three sizes,
twenty-four column widths) and sheet-fill-alignment.xlsx (the test fixture).
"""
import sys
import openpyxl
from openpyxl.styles import Alignment, Font
from openpyxl.utils import get_column_letter

out = sys.argv[1]

wb = openpyxl.Workbook(); ws = wb.active
widths = [3 + i * 1.37 for i in range(24)]
for c, w in enumerate(widths):
    ws.column_dimensions[get_column_letter(c + 1)].width = w
r = 1
for t, sz in [('-', 11), ('ab', 11), ('W', 11), ('iii', 11), (7, 11), ('x', 10), ('ab', 16), ('-', 8), ('Mw', 13)]:
    for c in range(len(widths)):
        cell = ws.cell(row=r, column=c + 1, value=t)
        cell.alignment = Alignment(horizontal='fill'); cell.font = Font(size=sz)
    r += 2
wb.save(f'{out}/fill2.xlsx')

wb = openpyxl.Workbook(); ws = wb.active
for c, w in enumerate([5, 12, 20]):
    ws.column_dimensions[get_column_letter(c + 1)].width = w
rows = [('-', {}), ('ab', {}), (7, {}), ('Fill me please', {}), (7, {'wrap_text': True})]
for r, (t, extra) in enumerate(rows, start=1):
    for c in range(3):
        ws.cell(row=r, column=c + 1, value=t).alignment = Alignment(horizontal='fill', **extra)
wb.save(f'{out}/sheet-fill-alignment.xlsx')
