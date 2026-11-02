# Renders the 36 shipped Custom decals (A-Z, 0-9) in Orbitron (SIL OFL 1.1) as 256x256 white-on-transparent PNGs.
import os
from PIL import Image, ImageFont, ImageDraw
here = os.path.dirname(os.path.abspath(__file__)); out = os.path.join(here, '..', 'Custom'); os.makedirs(out, exist_ok=True)
S = 4; T = 256 * S; CAP = int(0.75 * T); MAXW = int(0.96 * T)
def font(size):
    f = ImageFont.truetype(os.path.join(here, 'Orbitron.ttf'), size)
    try: f.set_variation_by_axes([800])
    except Exception: pass
    return f
# one size for all glyphs: cap height 75% of the tile, reduced if needed so the widest glyph fits MAXW
chars = [chr(c) for c in range(65, 91)] + [str(d) for d in range(10)]
f = font(400); b = f.getbbox('H'); size = int(400 * CAP / (b[3] - b[1]))
f = font(size); wmax = max(f.getbbox(c)[2] - f.getbbox(c)[0] for c in chars)
if wmax > MAXW: size = int(size * MAXW / wmax); f = font(size)
hb = f.getbbox('H'); y = (T - (hb[3] - hb[1])) / 2 - hb[1]  # shared baseline/cap line, from 'H'
for ch in chars:
    bb = f.getbbox(ch)
    m = Image.new('L', (T, T), 0); d = ImageDraw.Draw(m)
    d.text(((T - (bb[2] - bb[0])) / 2 - bb[0], y), ch, font=f, fill=255)
    a = m.resize((256, 256), Image.LANCZOS)
    img = Image.merge('RGBA', (a.point(lambda v: 255 if v else 0),) * 3 + (a,))
    name = '%03d_%s' % (chars.index(ch) + 1, ch)  # 001_A .. 026_Z, 027_0 .. 036_9
    img.save(os.path.join(out, name + '.png'))
print('ok', len(chars))
