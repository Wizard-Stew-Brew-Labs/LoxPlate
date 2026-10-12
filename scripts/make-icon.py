#!/usr/bin/env python3
"""Generate the LoxPlate Thunderstore icon (256x256) into packaging/icon.png.

Armored-plate motif: beveled steel plate, crossed leather straps, corner
rivets and a subtle ember rim (the Flametal tier accent). Deterministic -
re-run any time to regenerate byte-identical output.
"""
from PIL import Image, ImageDraw

SIZE = 256
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def rounded(draw, box, radius, fill):
    draw.rounded_rectangle(box, radius=radius, fill=fill)


# Outer plate frame (dark iron)
rounded(d, (10, 10, 246, 246), 42, (38, 36, 34, 255))
# Bevel: lighter top-left lip, darker bottom-right lip
d.line((52, 16, 204, 16), fill=(84, 80, 76, 255), width=5)
d.line((16, 52, 16, 204), fill=(84, 80, 76, 255), width=5)
d.line((52, 240, 204, 240), fill=(20, 19, 18, 255), width=5)
d.line((240, 52, 240, 204), fill=(20, 19, 18, 255), width=5)
# Inner steel plate
rounded(d, (28, 28, 228, 228), 32, (96, 98, 104, 255))
# Vertical brushed-steel sheen (subtle alternating columns)
for x in range(40, 216, 10):
    shade = 100 + ((x // 10) % 2) * 8
    d.line((x, 34, x, 222), fill=(shade, shade + 2, shade + 6, 255), width=3)
# Corner rivets
for cx, cy in ((48, 48), (208, 48), (48, 208), (208, 208)):
    d.ellipse((cx - 9, cy - 9, cx + 9, cy + 9), fill=(58, 60, 66, 255))
    d.ellipse((cx - 5, cy - 6, cx + 5, cy + 4), fill=(140, 144, 152, 255))
# Crossed leather straps (with stitch dashes)
strap = (94, 62, 40, 255)
stitch = (214, 176, 128, 255)
rounded(d, (18, 106, 238, 150), 10, strap)
rounded(d, (106, 18, 150, 238), 10, strap)
for x in range(30, 232, 18):
    d.line((x, 121, x + 8, 121), fill=stitch, width=3)
    d.line((121, x, 121, x + 8), fill=stitch, width=3)
# Central buckle where the straps meet
d.ellipse((96, 96, 160, 160), fill=(70, 72, 78, 255))
d.ellipse((106, 106, 150, 150), outline=(160, 164, 172, 255), width=6)
d.rectangle((124, 116, 132, 140), fill=(38, 40, 44, 255))
# Ember rim (Flametal accent) along the inside edge
for t in range(0, 360, 4):
    import math
    cx, cy = 128, 128
    r = 108
    a = math.radians(t)
    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
    d.point((x, y), fill=(255, 96, 24, 235))
    d.point((x - 2 * math.cos(a), y - 2 * math.sin(a)), fill=(200, 60, 16, 190))

img.save("packaging/icon.png")
print("wrote packaging/icon.png", img.size)
