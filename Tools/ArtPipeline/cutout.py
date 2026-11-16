"""White-background cutout for Gemini character art.

Usage: python cutout.py <src> <out_png> [preview_png]
- Background = near-white pixels connected to the image border.
- Body pixels stay fully opaque.
- Background + a thin edge band get "color to alpha" against white,
  which keeps the soft contact shadow and removes the white fringe.
"""
import sys
import numpy as np
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
preview = sys.argv[3] if len(sys.argv) > 3 else None

rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
h, w, _ = rgb.shape
mn, mx = rgb.min(axis=2), rgb.max(axis=2)
candidate = (mn > 215) & ((mx - mn) < 40)


def dilate(m):
    d = m.copy()
    d[1:] |= m[:-1]; d[:-1] |= m[1:]
    d[:, 1:] |= m[:, :-1]; d[:, :-1] |= m[:, 1:]
    return d


# flood fill from the border through candidate pixels
bg = np.zeros_like(candidate)
bg[0, :] = bg[-1, :] = bg[:, 0] = bg[:, -1] = True
bg &= candidate
while True:
    nxt = dilate(bg) & candidate
    if nxt.sum() == bg.sum():
        break
    bg = nxt

# the contact shadow below the body is tinted by the body color, so it fails
# the strict white test: loosen the test only under the body's lowest row
core = (mx - mn) > 100
rows = np.where(core.any(axis=1))[0]
if len(rows):
    below = np.zeros_like(bg)
    below[max(rows[-1] - 25, 0):] = True
    loose = below & (mn > 120) & ((mx - mn) < 110)
    cand2 = candidate | loose
    while True:
        nxt = dilate(bg) & cand2
        if nxt.sum() == bg.sum():
            break
        bg = nxt
    # small whitish pockets trapped between body and shadow (stricter test, wider zone)
    near = np.zeros_like(bg)
    near[max(rows[-1] - 50, 0):] = True
    bg |= (cand2 & below) | (near & (mn > 195) & ((mx - mn) < 60))

band = bg.copy()
for _ in range(2):
    band = dilate(band)

# color to alpha against white
a = ((255.0 - rgb) / 255.0).max(axis=2)
a = np.clip((a - 0.03) / 0.97, 0, 1)  # drop faint noise in the white
alpha = np.where(band, a, 1.0)
safe = np.maximum(alpha, 1e-4)[..., None]
col = np.where(band[..., None], (rgb - 255.0 * (1 - safe)) / safe, rgb)
col = np.clip(col, 0, 255)

rgba = np.dstack([col, alpha * 255]).astype(np.uint8)
Image.fromarray(rgba, "RGBA").save(out, optimize=True)
print("saved", out, "bg%", round(bg.mean() * 100, 1))

if preview:
    img = Image.fromarray(rgba, "RGBA")
    tiles = []
    for color in [(40, 30, 60), (90, 160, 90)]:
        base = Image.new("RGBA", (w, h), color + (255,))
        base.alpha_composite(img)
        tiles.append(base)
    sheet = Image.new("RGBA", (w * 2, h))
    for i, t in enumerate(tiles):
        sheet.paste(t, (w * i, 0))
    sheet.convert("RGB").resize((w, h // 2)).save(preview)
    print("preview", preview)
