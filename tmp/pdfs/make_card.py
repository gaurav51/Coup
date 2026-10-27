from reportlab.pdfgen import canvas
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from pathlib import Path
root=Path('/Users/gaurav-bs/Coup')
pdfmetrics.registerFont(TTFont('Body',str(root/'Assets/TextMesh Pro/Fonts/LiberationSans.ttf')))
pdfmetrics.registerFont(TTFont('Display',str(root/'Assets/Fonts/Cormorant_Garamond/static/CormorantGaramond-Bold.ttf')))
W,H=80*mm,120*mm
navy='#17243A'; gold='#A17B3E'; ink='#202B3A'; cream='#FBF7ED'
style=ParagraphStyle('body',fontName='Body',fontSize=7.9,leading=9.7,textColor=HexColor(ink))

def para(c,text,x,y,w=W-32,size=7.9):
    s=ParagraphStyle('p',parent=style,fontSize=size,leading=size*1.23)
    p=Paragraph(text,s); _,h=p.wrap(w,500); p.drawOn(c,x,y-h); return y-h

def label(c,text,x,y,size=9,color=navy):
    c.setFillColor(HexColor(color));c.setFont('Display',size);c.drawString(x,y,text)

def base(c,side,title,subtitle):
    c.setFillColor(HexColor(cream));c.rect(0,0,W,H,fill=1,stroke=0)
    c.setStrokeColor(HexColor(gold));c.setLineWidth(.7);c.rect(6,6,W-12,H-12,fill=0)
    c.setFillColor(HexColor(navy));c.rect(6,H-57,W-12,51,fill=1,stroke=0)
    c.setFillColor(HexColor('#F5E6BE'));c.setFont('Display',25);c.drawString(16,H-32,title)
    c.setFont('Body',6.7);c.drawString(17,H-45,subtitle)
    c.setStrokeColor(HexColor(gold));c.line(16,23,W-16,23)
    c.setFont('Body',5.7);c.setFillColor(HexColor(gold));c.drawString(16,14,'VEILED COURT  /  PERSONAL REFERENCE');c.drawRightString(W-16,14,side)

def front(c):
    base(c,'01','COUP','ACTIONS  /  CHOOSE ONE ON YOUR TURN')
    y=H-70
    label(c,'ANY PLAYER',16,y,9);y-=5
    for name,txt in [('INCOME','Take 1 coin. Cannot be blocked or challenged.'),('FOREIGN AID','Take 2 coins. Any Duke may block.'),('COUP','Pay 7 coins. Target loses 1 influence.<br/>Cannot be blocked or challenged.')]:
        y-=7;label(c,name,16,y,9);y=para(c,txt,16,y-3)-3
    y-=6;label(c,'CLAIM A ROLE  /  BLUFFING IS ALLOWED',16,y,8.6);y-=4
    entries=[('DUKE','#7B3FA0','Tax: take 3 coins. Blocks foreign aid.'),('CAPTAIN','#1E3A6E','Steal up to 2 coins from a player.<br/>Blocks stealing.'),('ASSASSIN','#1A1F38','Pay 3 coins. Target loses 1 influence.<br/>Blocked by Contessa.'),('AMBASSADOR','#D4791E','Draw 2 cards; choose which live cards to keep; return 2. Blocks stealing.'),('CONTESSA','#B52430','Blocks assassination. No special action.')]
    for name,col,txt in entries:
        y-=7;c.setFillColor(HexColor(col));c.rect(16,y-1,3,8,fill=1,stroke=0);label(c,name,24,y,9,col);y=para(c,txt,24,y-3,W-40)-3
    assert y>27,y

def back(c):
    base(c,'02','COURT RULES','SETUP  /  BLUFF  /  CHALLENGE  /  SURVIVE')
    y=H-70
    sections=[('SET UP & WIN','Deck: 3 of each role. Deal 2 hidden cards and 2 coins each. With 2 players, the first takes 1 coin. Last player with influence wins.'),('YOUR TURN','Take 1 action; play moves clockwise. At 10+ coins, you must Coup. Keep coins visible. You may bluff any role.'),('CHALLENGES','Anyone may challenge a role claim or block before it resolves. Income, foreign aid and Coup cannot be challenged.'),('PROVE YOUR CLAIM','Show the role: the challenger loses 1 influence. Shuffle your shown card into the deck, draw a replacement, then resolve the claim. Otherwise, you lose 1 influence; your claim fails. A challenged action that fails refunds its cost.'),('BLOCKS','Anyone may block foreign aid as Duke. Only the target may block stealing or assassination. A successful block stops the action; assassination coins stay spent. Blocks can be bluffed and challenged.'),('LOSING INFLUENCE','Reveal a hidden card permanently; it loses its powers. Lose both: you are out. A failed Assassin challenge can cost 1 influence, then assassination another.')]
    for title,txt in sections:
        label(c,title,16,y,9);y=para(c,txt,16,y-4,size=7.5)-8
    assert y+8>28,y

out=root/'output/pdf'
c=canvas.Canvas(str(out/'Coup-Reference-Card.pdf'),pagesize=(W,H));c.setTitle('Coup - double-sided reference card (80 x 120 mm)')
for f in (front,back):f(c);c.showPage()
c.save()
AW,AH=210*mm,297*mm
c=canvas.Canvas(str(out/'Coup-Reference-Card-A4.pdf'),pagesize=(AW,AH));c.setTitle('Coup - A4 print and cut reference card')
label(c,'COUP / PRINT & CUT',20*mm,AH-27*mm,23)
para(c,'Print at 100% / Actual size. Each side is 80 x 120 mm.<br/>Cut out both panels along the crop marks, then glue back-to-back.',20*mm,AH-34*mm,170*mm,10)
y=AH-175*mm
for x,f in [(20*mm,front),(110*mm,back)]:
    c.saveState();c.translate(x,y);f(c);c.restoreState()
    c.setStrokeColor(HexColor('#777777'));c.setLineWidth(.3)
    for xx,dx in [(x,-1),(x+W,1)]:
        for yy,dy in [(y,-1),(y+H,1)]:
            c.line(xx+dx*2,yy,xx+dx*8,yy);c.line(xx,yy+dy*2,xx,yy+dy*8)
para(c,'A custom reference for the base game. Keep the original rulebook for edge cases.<br/>Design: Veiled Court palette. Text remains sharp vector type for printing.',20*mm,y-22*mm,170*mm,9)
c.linkURL('https://indieboardsandcards.com/our-games/coup/',(20*mm,y-40*mm,190*mm,y-33*mm))
label(c,'Original game: Indie Boards & Cards / Rikki Tahta',20*mm,y-38*mm,10)
c.save()
print('Created both PDFs')
