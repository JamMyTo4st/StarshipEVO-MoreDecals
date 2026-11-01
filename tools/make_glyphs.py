# Generates the built-in MoreDecals glyphs: 256x256 RGBA, flat white on transparent.
# Letters/digits use Nunito (SIL OFL 1.1, tools/OFL-Nunito.txt) at Black weight; symbols are drawn with shapes.
import math, os, sys
from PIL import Image, ImageDraw, ImageFont
S=256; SS=4; B=S*SS
here=os.path.dirname(os.path.abspath(__file__)); out=os.path.join(here,'..','Glyphs')
font=ImageFont.truetype(os.path.join(here,'Nunito-Variable.ttf'), int(200*SS))
try: font.set_variation_by_axes([900])
except Exception as e: print('variation',e)
W=(255,255,255,255)
def canvas(): return Image.new('RGBA',(B,B),(255,255,255,0))
def finish(im,name): im.resize((S,S),Image.LANCZOS).save(os.path.join(out,name+'.png'))
def text(ch,name):
    im=canvas(); d=ImageDraw.Draw(im)
    l,t,r,b=d.textbbox((0,0),ch,font=font)
    d.text(((B-(r-l))/2-l,(B-(b-t))/2-t),ch,font=font,fill=W); finish(im,name)
def c(v): return v*SS
def sym(name,fn):
    im=canvas(); d=ImageDraw.Draw(im); fn(im,d); finish(im,name)
def plus(im,d): d.rounded_rectangle([c(108),c(36),c(148),c(220)],c(20),W); d.rounded_rectangle([c(36),c(108),c(220),c(148)],c(20),W)
def minus(im,d): d.rounded_rectangle([c(36),c(108),c(220),c(148)],c(20),W)
def star(im,d):
    p=[]
    for i in range(10):
        r=c(100) if i%2==0 else c(42); a=-math.pi/2+i*math.pi/5
        p.append((c(128)+r*math.cos(a),c(134)+r*math.sin(a)))
    d.polygon(p,W)
def chev2(im,d):
    for y in (40,120):
        d.line([(c(44),c(y+70)),(c(128),c(y)),(c(212),c(y+70))],W,c(30),joint='curve')
        for x,yy in ((44,y+70),(128,y),(212,y+70)): d.ellipse([c(x-15),c(yy-15),c(x+15),c(yy+15)],W)
def cross(im,d):
    d.ellipse([c(48),c(48),c(208),c(208)],None,W,c(18))
    for a in ((128,20,128,92),(128,164,128,236),(20,128,92,128),(164,128,236,128)): d.line([c(a[0]),c(a[1]),c(a[2]),c(a[3])],W,c(16))
    d.ellipse([c(118),c(118),c(138),c(138)],W)
def gear(im,d):
    for i in range(8):
        a=i*math.pi/4; ca,sa=math.cos(a),math.sin(a); pts=[]
        for dx,dy in ((-18,60),(18,60),(18,104),(-18,104)):
            pts.append((c(128)+c(dx)*ca-c(dy)*sa,c(128)+c(dx)*sa+c(dy)*ca))
        d.polygon(pts,W)
    d.ellipse([c(46),c(46),c(210),c(210)],W); d.ellipse([c(98),c(98),c(158),c(158)],(0,0,0,0))
def wrench(im,d):
    t=Image.new('RGBA',(B,B),(0,0,0,0)); e=ImageDraw.Draw(t)
    e.rounded_rectangle([c(112),c(80),c(144),c(236)],c(16),W)
    e.ellipse([c(72),c(20),c(184),c(132)],W); e.rectangle([c(112),c(10),c(144),c(70)],(0,0,0,0))
    e.ellipse([c(108),c(52),c(148),c(92)],(0,0,0,0))
    im.alpha_composite(t.rotate(-45,Image.BICUBIC,center=(B/2,B/2)))
def anchor(im,d):
    d.ellipse([c(108),c(20),c(148),c(60)],None,W,c(12))
    d.rounded_rectangle([c(116),c(56),c(140),c(220)],c(12),W)
    d.rounded_rectangle([c(76),c(76),c(180),c(98)],c(11),W)
    d.arc([c(40),c(100),c(216),c(228)],20,160,W,c(22))
    d.polygon([(c(28),c(150)),(c(70),c(150)),(c(48),c(118))],W); d.polygon([(c(228),c(150)),(c(186),c(150)),(c(208),c(118))],W)
def skull(im,d):
    d.ellipse([c(48),c(24),c(208),c(176)],W); d.rounded_rectangle([c(82),c(140),c(174),c(224)],c(16),W)
    z=(0,0,0,0)
    d.ellipse([c(74),c(86),c(118),c(132)],z); d.ellipse([c(138),c(86),c(182),c(132)],z)
    d.polygon([(c(128),c(140)),(c(116),c(164)),(c(140),c(164))],z)
    for x in (104,128,152): d.rectangle([c(x-4),c(196),c(x+4),c(224)],z)
def circled(ch):
    def f(im,d):
        d.ellipse([c(20),c(20),c(236),c(236)],W)
        g=ImageFont.truetype(os.path.join(here,'Nunito-Variable.ttf'),int(150*SS))
        try: g.set_variation_by_axes([900])
        except Exception: pass
        m=Image.new('L',(B,B),0); md=ImageDraw.Draw(m); l,t,r,b=md.textbbox((0,0),ch,font=g)
        md.text(((B-(r-l))/2-l,(B-(b-t))/2-t),ch,font=g,fill=255)
        a=im.getchannel('A'); a.paste(0,mask=m); im.putalpha(a)
    return f
def noentry(im,d):
    d.ellipse([c(20),c(20),c(236),c(236)],W); d.rounded_rectangle([c(56),c(110),c(200),c(146)],c(8),(0,0,0,0))
os.makedirs(out,exist_ok=True)
for ch in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ': text(ch,'letter_'+ch)
for ch in '0123456789': text(ch,'digit_'+ch)
for n,f in [('plus',plus),('minus',minus),('star',star),('chevron_double',chev2),('crosshair',cross),('gear',gear),('wrench',wrench),('anchor',anchor),('skull',skull),('info',circled('i')),('question',circled('?')),('no_entry',noentry)]: sym('sym_'+n,f)
print('ok')
