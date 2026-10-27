"""White role sigils for the rules card, traced from the emblems on the role portraits.

Drawn in a 256 design space, rendered 4x and downsampled for clean edges.
Usage: python make_sigils.py <out_dir> <preview_png>
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

SS = 4                     # supersample
D = 256 * SS               # design canvas
WHITE = 255


def canvas():
    return Image.new("L", (D, D), 0)


def s(points):
    return [(x * SS, y * SS) for x, y in points]


def duke(d):
    # Three-point crown with a cut gem, over a separate bar.
    d.polygon(s([(36, 176), (36, 64), (84, 122), (128, 40), (172, 122), (220, 64), (220, 176)]), fill=WHITE)
    d.polygon(s([(128, 100), (146, 130), (128, 160), (110, 130)]), fill=0)
    d.rectangle(s([(36, 194), (220, 216)]), fill=WHITE)


def assassin(d):
    # Split V of two blades with a dagger dropping between them.
    d.polygon(s([(22, 50), (90, 50), (122, 172), (122, 226)]), fill=WHITE)
    d.polygon(s([(234, 50), (166, 50), (134, 172), (134, 226)]), fill=WHITE)
    d.polygon(s([(114, 26), (142, 26), (134, 148), (128, 170), (122, 148)]), fill=WHITE)


def ambassador(d):
    # Globe: rim, meridian ellipse, equator and two parallels.
    w = 18 * SS
    d.ellipse(s([(30, 30), (226, 226)]), outline=WHITE, width=w)
    d.ellipse(s([(86, 30), (170, 226)]), outline=WHITE, width=14 * SS)
    d.rectangle(s([(30, 121), (226, 135)]), fill=WHITE)
    for y in (78, 178):
        d.rectangle(s([(52, y - 6), (204, y + 6)]), fill=WHITE)


def captain(d):
    # Navigator's compass: ring, shaft and three outward points.
    d.ellipse(s([(58, 58), (198, 198)]), outline=WHITE, width=20 * SS)
    d.rectangle(s([(118, 18), (138, 200)]), fill=WHITE)
    d.polygon(s([(66, 112), (66, 144), (14, 128)]), fill=WHITE)
    d.polygon(s([(190, 112), (190, 144), (242, 128)]), fill=WHITE)
    d.polygon(s([(112, 190), (144, 190), (128, 242)]), fill=WHITE)


def contessa(d):
    # Diamond outline cradled by two chevrons.
    d.line(s([(128, 16), (172, 84), (128, 152), (84, 84), (128, 16)]), fill=WHITE, width=18 * SS, joint="curve")
    d.polygon(s([(64, 112), (128, 176), (192, 112), (192, 140), (128, 204), (64, 140)]), fill=WHITE)
    d.polygon(s([(30, 134), (128, 232), (226, 134), (226, 160), (128, 256 - 2), (30, 160)]), fill=WHITE)


def none_mark(d):
    # Heavy cross for "no counteraction".
    w = 44 * SS
    d.line(s([(46, 46), (210, 210)]), fill=WHITE, width=w)
    d.line(s([(210, 46), (46, 210)]), fill=WHITE, width=w)


SIGILS = {
    "Sigil_Duke": (duke, 96),
    "Sigil_Assassin": (assassin, 96),
    "Sigil_Ambassador": (ambassador, 96),
    "Sigil_Captain": (captain, 96),
    "Sigil_Contessa": (contessa, 96),
    "Mark_None": (none_mark, 48),
}


def render(draw_fn, size):
    mask = canvas()
    draw_fn(ImageDraw.Draw(mask))
    mask = mask.resize((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(mask)
    return out


def main(out_dir, preview):
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    accents = ["#7B3FA0", "#1A1F38", "#D4791E", "#1E3A6E", "#B52430", "#17243A"]
    sheet = Image.new("RGBA", (6 * 200, 400), (23, 36, 58, 255))
    for i, (name, (fn, size)) in enumerate(SIGILS.items()):
        img = render(fn, size)
        img.save(out_dir / f"{name}.png")
        # Large check plus actual-size check, on the role colour.
        tile = Image.new("RGBA", (200, 200), accents[i])
        tile.alpha_composite(render(fn, 180), (10, 10))
        sheet.paste(tile, (i * 200, 0))
        small = Image.new("RGBA", (200, 200), accents[i])
        small.alpha_composite(img.resize((52, 52), Image.BILINEAR), (74, 74))
        sheet.paste(small, (i * 200, 200))
    sheet.save(preview)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
