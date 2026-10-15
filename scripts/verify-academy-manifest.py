#!/usr/bin/env python3
"""Read-only integrity check of the 50 owner-approved Academy references.

This validates the approved local reference catalog, NOT a live generation,
provider authorization, copyright clearance beyond the recorded declaration,
human visual-quality signoff, or campaign readiness.
"""
import argparse
import csv
from datetime import date
from pathlib import Path


REQUIRED_COLUMNS = (
    "referenceId", "nicheNumber", "nicheKey", "nicheName", "expectedFile",
    "lifecycle", "status", "learn", "doNotCopy", "mappingNote",
    "provenanceSource", "ownership", "approvedOn",
    "approvingAuthority", "permittedInternalUse",
)


def verify_catalog(root: Path) -> dict[str, int]:
    catalog = root / "assets" / "alpha-prototypes" / "creative-academy"
    manifest = catalog / "MANIFEST.tsv"
    inbox = catalog / "inbox"
    if not manifest.is_file() or manifest.is_symlink() or not inbox.is_dir():
        raise ValueError("Approved catalog manifest or inbox is missing/unsafe")

    with manifest.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle, delimiter="\t")
        if tuple(reader.fieldnames or ()) != REQUIRED_COLUMNS:
            raise ValueError("Academy manifest schema changed without review")
        rows = list(reader)

    if len(rows) != 50:
        raise ValueError(f"Expected 50 approved Academy niches; found {len(rows)}")

    used_ids: set[str] = set()
    used_keys: set[str] = set()
    used_files: set[str] = set()
    for index, row in enumerate(rows, 1):
        ref = row["referenceId"].strip()
        filename = row["expectedFile"]
        if row["nicheNumber"] != str(index) or ref != f"ACA-{index:03d}-V1":
            raise ValueError(f"Catalog reference/order mismatch at niche {index}")
        if ref in used_ids or row["nicheKey"] in used_keys or filename in used_files:
            raise ValueError(f"Duplicate niche, reference ID or image mapping: {ref}")
        used_ids.add(ref)
        used_keys.add(row["nicheKey"])
        used_files.add(filename)

        for field in REQUIRED_COLUMNS:
            if row[field] is None or not row[field].strip():
                raise ValueError(f"{ref}: missing required {field}")
        if row["lifecycle"] != "ACTIVE" or row["status"] != "UPLOADED":
            raise ValueError(f"{ref}: owner-approved reference is no longer ACTIVE/UPLOADED")
        if (row["provenanceSource"], row["ownership"], row["approvingAuthority"]) != (
            "owner-upload", "owner", "owner"
        ):
            raise ValueError(f"{ref}: owner provenance or approval changed")
        if row["permittedInternalUse"] != "Academy quality reference":
            raise ValueError(f"{ref}: non-reference use is not authorized by manifest")
        try:
            date.fromisoformat(row["approvedOn"])
        except ValueError as error:
            raise ValueError(f"{ref}: approval date is invalid") from error

        if Path(filename).name != filename or filename in {".", ".."}:
            raise ValueError(f"{ref}: unsafe image path")
        path = inbox / filename
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"{ref}: image is missing or is a symbolic link")
        with path.open("rb") as handle:
            header = handle.read(8)
        extension = path.suffix.lower()
        if extension in {".jpeg", ".jpg"}:
            valid = header.startswith(b"\xff\xd8\xff")
        elif extension == ".png":
            valid = header == b"\x89PNG\r\n\x1a\n"
        else:
            valid = False
        if not valid:
            raise ValueError(f"{ref}: image format/signature does not match file extension")

    return {"approved_references": len(rows), "mapped_local_images": len(used_files)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    result = verify_catalog(args.root)
    print(
        "PASS: owner-approved Academy reference catalog: "
        f"{result['approved_references']}/50 ACTIVE, "
        f"{result['mapped_local_images']}/50 mapped local images. "
        "Provider usage, visual QA and delivery remain separately gated."
    )


if __name__ == "__main__":
    main()
