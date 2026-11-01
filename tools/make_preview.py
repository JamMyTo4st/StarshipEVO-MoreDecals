import os
from PIL import Image, ImageDraw
here=os.path.dirname(os.path.abspath(__file__)); g=os.path.join(here,'..','Glyphs')
order=[f'letter_{c}' for c in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ']+[f'digit_{c}' for c in '0123456789']+['sym_'+n for n in 'plus minus star chevron_double crosshair gear wrench anchor skull info question no_entry'.split()]
cols=12; cell=128; rows=(len(order)+cols-1)//cols
im=Image.new('RGBA',(cols*cell,rows*(cell+18)),(40,44,52,255)); d=ImageDraw.Draw(im)
for i,n in enumerate(order):
    x,y=(i%cols)*cell,(i//cols)*(cell+18)
    im.alpha_composite(Image.open(os.path.join(g,n+'.png')).resize((cell-16,cell-16)),(x+8,y+4))
    d.text((x+6,y+cell-8),f'{i+1} {n.split("_",1)[1]}',fill=(200,200,200,255))
im.save('/workspace/mods/out/MoreDecals-preview.png')
