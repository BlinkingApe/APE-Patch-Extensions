#!/usr/bin/env python3
"""Build APE's application icon from the Blinking Ape portrait.

The source art is 187x251 -- portrait, not square -- so it is padded to a square
canvas rather than stretched, which keeps the ape undistorted at 16 px where any
squash is most visible.

Writes a multi-resolution .ico (16, 32, 48, 256) beside the source.  The exe
binds it at build time, which drives the Explorer thumbnail and the taskbar
button; the form loads the same file from its resources, which drives the title
bar and the alt-tab entry.  Both are needed -- setting only one leaves the other
on the default .NET icon.
"""

import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "data", "art", "blinkingape.png")
OUT = os.path.join(ROOT, "data", "art", "blinkingape.ico")
SIZES = [16, 32, 48, 256]


def main():
    if not os.path.exists(SRC):
        print("make_icon: %s is missing" % SRC, file=sys.stderr)
        return 1

    art = Image.open(SRC).convert("RGBA")
    side = max(art.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.paste(art, ((side - art.width) // 2, (side - art.height) // 2), art)

    # Pillow's ICO writer resamples to each requested size itself; going through
    # a high-quality resize first keeps the small sizes from turning to mush.
    square = square.resize((256, 256), Image.LANCZOS)
    square.save(OUT, format="ICO", sizes=[(s, s) for s in SIZES])
    print("make_icon: wrote %s (%s)" % (OUT, ", ".join("%dpx" % s for s in SIZES)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
