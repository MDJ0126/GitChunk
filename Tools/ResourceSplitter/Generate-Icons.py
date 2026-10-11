"""Generate the shared geometric app icon (requires Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).parent / "Assets"
root.mkdir(exist_ok=True)
n = 1024
image = Image.new("RGBA", (n, n))
pixels = image.load()
for y in range(n):
    for x in range(n):
        t = (x + y) / (2 * n)
        pixels[x, y] = (int(42 - 19*t), int(114 - 50*t), int(248 - 35*t), 255)
mask = Image.new("L", (n, n))
ImageDraw.Draw(mask).rounded_rectangle((24, 24, 1000, 1000), radius=224, fill=255)
image.putalpha(mask)
shadow = Image.new("RGBA", (n,n))
d = ImageDraw.Draw(shadow)
for rect in [(220, 246, 488, 514), (220, 550, 488, 818), (530, 396, 798, 664)]:
    d.rounded_rectangle(rect, radius=58, fill=(7, 24, 88, 100))
image = Image.alpha_composite(image, shadow.filter(ImageFilter.GaussianBlur(26)))
d = ImageDraw.Draw(image)
# Two pieces align into a single piece: the same mark represents split and join.
d.rounded_rectangle((214, 216, 478, 480), radius=54, fill=(255,255,255))
d.rounded_rectangle((214, 524, 478, 788), radius=54, fill=(155,236,244))
d.rounded_rectangle((554, 370, 818, 634), radius=54, fill=(255,255,255))
# Subtle slots recall stacked resource files while staying legible at small sizes.
for y in (292, 330, 368):
    d.rounded_rectangle((282,y,410,y+15), radius=7, fill=(60,114,224))
for y in (600,638,676):
    d.rounded_rectangle((282,y,410,y+15), radius=7, fill=(31,130,161))
for y in (446,484,522):
    d.rounded_rectangle((622,y,750,y+15), radius=7, fill=(60,114,224))
image.save(root / "AppIcon.png")
image.save(root / "AppIcon.ico", sizes=[(s,s) for s in (16,24,32,48,64,128,256)])
iconset = root / "AppIcon.iconset"
iconset.mkdir(exist_ok=True)
for size in (16,32,128,256,512):
    for scale in (1,2):
        suffix = "@2x" if scale == 2 else ""
        image.resize((size*scale,size*scale), Image.Resampling.LANCZOS).save(iconset / f"icon_{size}x{size}{suffix}.png")
print("Generated Windows ICO, shared PNG, and macOS iconset.")
