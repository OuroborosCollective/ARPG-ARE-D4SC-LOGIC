from pathlib import Path
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,Table,TableStyle
from reportlab.lib.styles import getSampleStyleSheet
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from xml.sax.saxutils import escape
import re
pdfmetrics.registerFont(TTFont('DejaVu','/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'))
pdfmetrics.registerFont(TTFont('DejaVu-Bold','/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'))
pdfmetrics.registerFontFamily('DejaVu',normal='DejaVu',bold='DejaVu-Bold',italic='DejaVu',boldItalic='DejaVu-Bold')
src=Path('docs/handoffs/Ashen_Crypt_Handoff_20261115.md');out=Path('/workspace/artifacts/ashen-game-handoff-20261115/Ashen_Crypt_Handoff_20261115.pdf')
styles=getSampleStyleSheet()
for name in ['BodyText','Title','Heading1','Heading2']:
 styles[name].fontName='DejaVu-Bold' if name!='BodyText' else 'DejaVu'
styles['BodyText'].fontSize=9;styles['BodyText'].leading=13;styles['BodyText'].splitLongWords=True
flow=[];table=[]
def fmt(x):return re.sub(r'\*\*(.+?)\*\*',r'<b>\1</b>',escape(x.replace('`','')))
def flush():
 global table
 if table:
  rows=[[Paragraph(fmt(c),styles['BodyText']) for c in row] for row in table]
  t=Table(rows,colWidths=[170,345],repeatRows=1);t.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#e1eeee')),('GRID',(0,0),(-1,-1),0.3,colors.grey),('BOTTOMPADDING',(0,0),(-1,-1),7)]));flow.extend([t,Spacer(1,10)]);table=[]
for line in src.read_text().splitlines():
 if line.startswith('|'):
  row=[c.strip() for c in line.strip('|').split('|')]
  if all(re.fullmatch(r'[-: ]+',c) for c in row):continue
  table.append(row);continue
 flush()
 if not line.strip():flow.append(Spacer(1,6));continue
 if line.startswith('# '):flow.append(Paragraph(fmt(line[2:]),styles['Title']))
 elif line.startswith('## '):flow.append(Paragraph(fmt(line[3:]),styles['Heading1']))
 elif line.startswith('### '):flow.append(Paragraph(fmt(line[4:]),styles['Heading2']))
 else:flow.append(Paragraph(fmt(line.removeprefix('> ')),styles['BodyText']))
flush()
def footer(c,d):
 c.setFont('DejaVu',8);c.drawString(40,25,'Ashen Crypt · Verlaufshandoff · 15.11.2026');c.drawRightString(555,25,str(d.page))
SimpleDocTemplate(str(out),pagesize=A4,leftMargin=40,rightMargin=40,topMargin=40,bottomMargin=40,title='Ashen Crypt – Verlaufshandoff',author='Ouroboros Collective').build(flow,onFirstPage=footer,onLaterPages=footer)
print(out)
