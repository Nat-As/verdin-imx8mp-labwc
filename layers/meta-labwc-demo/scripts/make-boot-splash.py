#!/usr/bin/env python3
"""Generate the images of the "argos" Plymouth boot splash theme.

  make-boot-splash.py <wallpaper.png> <output dir> [--preview preview.png]

- watermark.png: the Alakai logo, cut out of the desktop wallpaper onto a
  transparent background (the splash background is black).
- throbber-0001.png ... throbber-0030.png: a ring of rays in the logo's red,
  like the logo's starburst, whose bright head goes round once per second
  (Plymouth plays throbber frames at 30 frames per second and loops them).

Needs Python 3 with Pillow. The generated images are committed to the layer
(recipes-graphics/argos-boot-splash/files/theme), so builds don't need Pillow.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

# Logo area in the 1376x768 wallpaper, and the logo's red
LOGO_BOX = (340, 288, 1022, 472)
RED = (183, 5, 5)
SPINNER_RED = (220, 24, 24)  # a little brighter: the tail fades on black

FRAMES = 30          # one rotation per second at 30 fps
RAYS = 12
SIZE = 72            # throbber size in pixels
SUPERSAMPLE = 4


def make_watermark(wallpaper, out):
    im = Image.open(wallpaper).convert("RGB").crop(LOGO_BOX)
    w, h = im.size
    src = im.load()
    logo = Image.new("RGBA", (w, h))
    dst = logo.load()
    # Background and the wallpaper's faint shapes are below ~90; the white
    # text and the red line/starburst are well above
    lo, hi = 90.0, 170.0
    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            a = (max(r, g, b) - lo) / (hi - lo)
            a = 0.0 if a < 0 else 1.0 if a > 1 else a
            dst[x, y] = (r, g, b, int(round(a * 255)))
    bbox = logo.getbbox()
    logo = logo.crop(bbox)
    logo.save(out, optimize=True)
    return logo


def make_throbber(outdir):
    s = SIZE * SUPERSAMPLE
    c = s / 2
    r_in, r_out = s * 0.24, s * 0.46
    width = s * 0.085
    frames = []
    for f in range(FRAMES):
        img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        head = f * RAYS / FRAMES            # continuous head position
        for i in range(RAYS):
            behind = (head - i) % RAYS      # 0 = head, grows along the tail
            alpha = max(0.22, 1.0 - behind / (RAYS * 0.75))
            ang = 2 * math.pi * i / RAYS - math.pi / 2
            x0, y0 = c + r_in * math.cos(ang), c + r_in * math.sin(ang)
            x1, y1 = c + r_out * math.cos(ang), c + r_out * math.sin(ang)
            col = SPINNER_RED + (int(alpha * 255),)
            d.line([(x0, y0), (x1, y1)], fill=col, width=int(width))
            # round caps
            rr = width / 2
            for (px, py) in ((x0, y0), (x1, y1)):
                d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=col)
        img = img.resize((SIZE, SIZE), Image.LANCZOS)
        name = os.path.join(outdir, "throbber-%04d.png" % (f + 1))
        img.save(name, optimize=True)
        frames.append(img)
    return frames


def main():
    args = sys.argv[1:]
    preview = None
    if "--preview" in args:
        i = args.index("--preview")
        preview = args[i + 1]
        del args[i:i + 2]
    if len(args) != 2:
        sys.exit(__doc__)
    wallpaper, outdir = args
    os.makedirs(outdir, exist_ok=True)
    logo = make_watermark(wallpaper, os.path.join(outdir, "watermark.png"))
    frames = make_throbber(outdir)
    if preview:
        # The splash on the 1024x600 panel, with the alignments of argos.plymouth
        w, h = 1024, 600
        img = Image.new("RGBA", (w, h), (0, 0, 0, 255))
        lx = int(w * 0.5 - logo.width * 0.5)
        ly = int(h * 0.42 - logo.height * 0.42)
        img.alpha_composite(logo, (lx, ly))
        t = frames[0]
        img.alpha_composite(t, (int(w * 0.5 - t.width * 0.5), int(h * 0.75 - t.height * 0.75)))
        img.convert("RGB").save(preview)


if __name__ == "__main__":
    main()
