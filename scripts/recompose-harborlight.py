#!/usr/bin/env python3
"""Create the ARE-P01 test placement preview from one generated original.

LEFT_VERTICAL contains the original brand panel without stretching it.
BOTTOM_FULL is a purpose-built 1280x180 banner: the original lighthouse mark
plus the complete headline. It is not a crop through the photograph.
"""

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CANVAS = (1920, 1080)
LEFT_VERTICAL = (0, 0, 320, 1080)
BOTTOM_FULL = (320, 900, 1600, 1080)
PROTECTED_CENTER = (320, 0, 1600, 900)
BRAND_PANEL = (0, 0, 510, 720)
LIGHTHOUSE_MARK = (155, 78, 385, 245)
NAVY = (2, 20, 42)
CORAL = (239, 107, 86)
IVORY = (245, 236, 220)
TEAL = (126, 196, 196)
SANS = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
SANS_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def flatten_navy(image: Image.Image) -> Image.Image:
    copy = image.copy()
    pixels = copy.load()
    for y in range(copy.height):
        for x in range(copy.width):
            red, green, blue = pixels[x, y]
            if red < 45 and green < 75 and blue < 100 and blue >= red:
                pixels[x, y] = NAVY
    return copy


def contain(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    copy = image.copy()
    copy.thumbnail(size, Image.Resampling.LANCZOS)
    return copy


def tracked_text(
    draw: ImageDraw.ImageDraw,
    origin: tuple[int, int],
    text: str,
    font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    tracking: int,
) -> None:
    x, y = origin
    for character in text:
        draw.text((x, y), character, font=font, fill=fill)
        x += int(font.getlength(character)) + tracking


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: recompose-harborlight.py INPUT OUTPUT")

    source = Image.open(sys.argv[1]).convert("RGB")
    canvas = Image.new("RGB", CANVAS, NAVY)
    draw = ImageDraw.Draw(canvas)

    draw.rectangle(PROTECTED_CENTER, fill="#20242b")
    center_font = ImageFont.truetype(SANS, 28)
    center_text = "CREATOR CONTENT PROTECTED\nTEST PREVIEW - NOT DELIVERED"
    box = draw.multiline_textbbox((0, 0), center_text, font=center_font, align="center", spacing=12)
    text_size = (box[2] - box[0], box[3] - box[1])
    draw.multiline_text(
        (
            (PROTECTED_CENTER[0] + PROTECTED_CENTER[2] - text_size[0]) / 2,
            (PROTECTED_CENTER[1] + PROTECTED_CENTER[3] - text_size[1]) / 2,
        ),
        center_text,
        fill="#d5dae1",
        font=center_font,
        align="center",
        spacing=12,
    )

    brand_panel = contain(source.crop(BRAND_PANEL), (LEFT_VERTICAL[2] - 24, LEFT_VERTICAL[3] - 80))
    canvas.paste(
        brand_panel,
        (
            (LEFT_VERTICAL[2] - brand_panel.width) // 2,
            (LEFT_VERTICAL[3] - brand_panel.height) // 2,
        ),
    )

    banner = Image.new("RGB", (1280, 180), NAVY)
    banner_draw = ImageDraw.Draw(banner)
    banner_draw.rectangle((0, 0, 1279, 5), fill=CORAL)
    mark = contain(flatten_navy(source.crop(LIGHTHOUSE_MARK)), (150, 148))
    banner.paste(mark, (24, (180 - mark.height) // 2))

    word_font = ImageFont.truetype(SANS_BOLD, 22)
    headline_font = ImageFont.truetype(SANS, 34)
    place_font = ImageFont.truetype(SANS, 18)
    text_x = 24 + mark.width + 36
    tracked_text(banner_draw, (text_x, 36), "HARBORLIGHT PHARMACY", word_font, CORAL, 2)
    banner_draw.text(
        (text_x, 78),
        "Prescription pickup, ready when you are.",
        font=headline_font,
        fill=IVORY,
    )
    banner_draw.text((text_x, 128), "Panama City", font=place_font, fill=TEAL)
    canvas.paste(banner, (BOTTOM_FULL[0], BOTTOM_FULL[1]))

    label_font = ImageFont.truetype(SANS, 16)
    draw.rectangle(LEFT_VERTICAL, outline="#ffffff", width=3)
    draw.rectangle(BOTTOM_FULL, outline="#ffffff", width=3)
    draw.text((12, 14), "LEFT_VERTICAL 320x1080", fill="#ffffff", font=label_font)
    draw.text((1336, 868), "BOTTOM_FULL 1280x180", fill="#ffffff", font=label_font)

    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=True)


if __name__ == "__main__":
    main()
