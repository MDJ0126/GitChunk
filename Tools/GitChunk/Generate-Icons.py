"""Generate app icons from the README symbol (requires Pillow)."""
import base64
from io import BytesIO
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image

project = Path(__file__).resolve().parents[2]
source = project / 'docs/assets/git-chunk-symbol.svg'
svg = ET.parse(source).getroot()
x, y, width, height = map(float, svg.attrib['viewBox'].split())
embedded = svg.find('{http://www.w3.org/2000/svg}image')
if embedded is None:
    raise ValueError('README symbol must contain an embedded PNG image')
href = embedded.attrib['href']
if not href.startswith('data:image/png;base64,'):
    raise ValueError('README symbol must contain a base64 PNG image')
with Image.open(BytesIO(base64.b64decode(href.split(',', 1)[1]))) as original:
    # Match the SVG viewport used by all README variants exactly.
    sx = original.width / float(embedded.attrib['width'])
    sy = original.height / float(embedded.attrib['height'])
    symbol = original.convert('RGBA').crop((round(x*sx), round(y*sy), round((x+width)*sx), round((y+height)*sy)))

root = Path(__file__).parent / 'Assets'
root.mkdir(exist_ok=True)
image = Image.new('RGBA', (1024, 1024))
symbol = symbol.resize((round(symbol.width * 960 / max(symbol.size)), round(symbol.height * 960 / max(symbol.size))), Image.Resampling.LANCZOS)
image.alpha_composite(symbol, ((1024-symbol.width)//2, (1024-symbol.height)//2))
image.save(root / 'AppIcon.png')
image.save(root / 'AppIcon.ico', sizes=[(s,s) for s in (16,24,32,48,64,128,256)])
iconset = root / 'AppIcon.iconset'
iconset.mkdir(exist_ok=True)
for size in (16,32,128,256,512):
    for scale in (1,2):
        suffix = '@2x' if scale == 2 else ''
        image.resize((size*scale,size*scale), Image.Resampling.LANCZOS).save(iconset / f'icon_{size}x{size}{suffix}.png')
print(f'Generated Windows ICO, shared PNG, and macOS iconset from {source.name}.')
