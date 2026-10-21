"""[COLOR n] in an xlsx format code against 26.8.1.1: python3 -I make-xlcolour.py <outdir>.

26.8.1.1 paints [Color10]/[Color21]/[Color24]/[Color64]/[COLOR 21] in standard.soc entries
10/21/24/64/21 and prints [Color65]0.000 in the standard format (1004.5).
"""
import sys
import openpyxl
from openpyxl.styles import Font
wb = openpyxl.Workbook(); ws = wb.active
ws.column_dimensions['A'].width = 40
codes = ['[Color10]0.000', '[Color21]0.000', '[Color24]0.000', '[Color64]0.000', '[Color65]0.000',
         '[COLOR 21]0.000', '0.000']
for i, c in enumerate(codes):
    cell = ws.cell(row=i + 1, column=1, value=1000.5 + i); cell.number_format = c; cell.font = Font(size=24)
wb.save(f'{sys.argv[1]}/xlcolour.xlsx')
