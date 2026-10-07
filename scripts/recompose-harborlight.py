#!/usr/bin/env python3
"""Create the ARE-P01 test placement preview from one generated original."""

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CANVAS = (1920, 1080)
LEFT_VERTICAL = (0, 0, 320, 1080)
BOTTOM_FULL = (320, 900, 1600, 1080)
PROTECTED_CENTER = (320, 0, 1600, 900)


def contain(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    copy = image.copy()
    copy.thumbnail(size, Image.Resampling.LANCZOS)
    return copy


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: recompose-harborlight.py INPUT OUTPUT")

    source = Image.open(sys.argv[1]).convert("RGB")
    canvas = Image.new("RGB", CANVAS, "#081b33")
    draw = ImageDraw.Draw(canvas)

    # The center represents protected creator content and is intentionally not
    # filled by the generated advertisement.
    draw.rectangle(PROTECTED_CENTER, fill="#20242b")
    font = ImageFont.load_default(size=28)
    center_text = "CREATOR CONTENT PROTECTED\nTEST PREVIEW - NOT DELIVERED"
    box = draw.multiline_textbbox((0, 0), center_text, font=font, align="center", spacing=12)
    text_size = (box[2] - box[0], box[3] - box[1])
    draw.multiline_text(
        ((PROTECTED_CENTER[0] + PROTECTED_CENTER[2] - text_size[0]) / 2,
         (PROTECTED_CENTER[1] + PROTECTED_CENTER[3] - text_size[1]) / 2),
        center_text,
        fill="#d5dae1",
        font=font,
        align="center",
        spacing=12,
    )

    # Preserve the original brand panel without stretching it. The remaining
    # vertical area keeps the same navy brand field.
    brand_panel = source.crop((0, 0, source.width * 43 // 100, source.height))
    brand_panel = contain(brand_panel, (LEFT_VERTICAL[2], LEFT_VERTICAL[3]))
    canvas.paste(
        brand_panel,
        ((LEFT_VERTICAL[2] - brand_panel.width) // 2,
         (LEFT_VERTICAL[3] - brand_panel.height) // 2),
    )

    # Fill the bottom slot with a center crop from the original 16:9 creative.
    resized = source.resize((1280, 720), Image.Resampling.LANCZOS)
    banner = resized.crop((0, 270, 1280, 450))
    canvas.paste(banner, (BOTTOM_FULL[0], BOTTOM_FULL[1]))

    # Exact slot boundaries make this an auditable placement preview.
    draw.rectangle(LEFT_VERTICAL, outline="#ffffff", width=3)
    draw.rectangle(BOTTOM_FULL, outline="#ffffff", width=3)
    draw.text((10, 10), "LEFT_VERTICAL 320x1080", fill="#ffffff", font=ImageFont.load_default(size=18))
    draw.text((330, 910), "BOTTOM_FULL 1280x180", fill="#ffffff", font=ImageFont.load_default(size=18))

    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=True)


if __name__ == "__main__":
    main()
