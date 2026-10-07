#!/usr/bin/env python3
"""Measure HV-001 against the four retrieved references.

The distances are stored. They are not a quality grade or a copy judgment.
"""

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "assets/alpha-prototypes/creative-academy/harborlight/HV-001-evidence.json"
INBOX = ROOT / "assets/alpha-prototypes/creative-academy/inbox"
REFERENCES = (
    ("ACA-001-V1", "Pharmacy.jpeg"),
    ("ACA-002-V1", "Market.jpeg"),
    ("ACA-006-V1", "Restaurant.jpeg"),
    ("ACA-008-V1", "Auto shop.jpeg"),
)
METHOD = "32x18 grayscale mean absolute error and 8x8 average-hash Hamming distance"
NOTICE = (
    "These distances are measurements against the four retrieved references. "
    "They are not a quality grade, a copy judgment, or a visual QA result."
)


def pixels(image: Image.Image) -> list[int]:
    reader = getattr(image, "get_flattened_data", None)
    data = reader() if reader is not None else image.getdata()
    return [int(value) for value in data]


def grayscale(path: Path, size: tuple[int, int]) -> list[int]:
    image = ImageOps.grayscale(Image.open(path).convert("RGB")).resize(size, Image.Resampling.BOX)
    return pixels(image)


def mean_absolute_error(left: Path, right: Path) -> float:
    left_pixels = grayscale(left, (32, 18))
    right_pixels = grayscale(right, (32, 18))
    total = sum(abs(a - b) for a, b in zip(left_pixels, right_pixels, strict=True))
    return round(total / len(left_pixels), 2)


def average_hash(path: Path) -> tuple[int, ...]:
    values = grayscale(path, (8, 8))
    average = sum(values) / len(values)
    return tuple(1 if value >= average else 0 for value in values)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def measure(evidence: dict) -> dict:
    original = ROOT / evidence["original"]["path"]
    original_hash = sha256(original)
    if original_hash != evidence["original"]["sha256"]:
        raise SystemExit("The original hash does not match the evidence record.")
    original_bits = average_hash(original)
    distances = []
    for reference_id, filename in REFERENCES:
        path = INBOX / filename
        bits = average_hash(path)
        distances.append({
            "referenceId": reference_id,
            "file": filename,
            "sha256": sha256(path),
            "meanAbsoluteError": mean_absolute_error(original, path),
            "averageHashDistance": sum(left != right for left, right in zip(original_bits, bits, strict=True)),
        })
    return {
        "status": "MEASURED",
        "judgment": "NOT_JUDGED",
        "visualGrade": "NOT_ASSIGNED",
        "method": METHOD,
        "originalSha256": original_hash,
        "references": distances,
        "notice": NOTICE,
    }


def main() -> None:
    evidence = json.loads(EVIDENCE.read_text(encoding="utf-8"))
    evidence["pixelSimilarity"] = measure(evidence)
    evidence["checks"]["pixelSimilarity"] = "MEASURED"
    EVIDENCE.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
