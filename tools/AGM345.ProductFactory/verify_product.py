#!/usr/bin/env python3
"""Fail-closed offline contract for a real generated demo advert product."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def digest(path):
    with path.open("rb") as f:
        h=hashlib.sha256()
        for chunk in iter(lambda: f.read(1024*1024), b""):
            h.update(chunk)
    return h.hexdigest()


def inspect(directory):
    directory = Path(directory)
    receipt_file = directory / "product-receipt.json"
    data = json.loads(receipt_file.read_text(encoding="utf-8"))
    assert data["schema"] == "ALPHA-345-OFFLINE-FINISHED-AD-PRODUCT/V1"
    assert data["mode"] == "OFFLINE_SYNTHETIC_ONLY"
    assert data["product_id"] == "ARE-P01"
    assert data["geometry_version"] == "ARE-GEO-V1"
    assert data["commercial_approval"] is False
    assert data["creator_advertiser_rights_verified"] is False
    assert data["can_state_price"] is False
    assert data["reservation_authorized"] is False
    assert data["payment_authorized"] is False
    assert data["real_impressions"] == 0
    assert data["delivery_status"] == "NOT_SENT"
    assert data["quote_status"] == "PRICING_UNAVAILABLE"
    assert "FICTIONAL" in data["notice"] and "NOT A LIVE" in data["notice"]
    assert data["sponsor_slots"] == {
        "LEFT_VERTICAL":{"x":0,"y":0,"width":320,"height":1080},
        "BOTTOM_FULL":{"x":320,"y":900,"width":1280,"height":180}
    }
    assert data["protected_creator_area"] == {"x":320,"y":180,"width":1280,"height":720}
    video = data["media"]["video"]
    poster = data["media"]["poster"]
    assert (video["width"],video["height"],video["fps"]) == (1920,1080,12)
    assert (poster["width"],poster["height"]) == (1920,1080)
    assert video["mime"] == "video/mp4"
    assert poster["mime"] == "image/png"
    for medium in (video,poster):
        assert "/" not in medium["file"] and "\\" not in medium["file"]
        file_path = directory/medium["file"]
        assert file_path.is_file() and file_path.stat().st_size > 3000
        assert medium["bytes"] == file_path.stat().st_size
        assert medium["sha256"] == digest(file_path)
    assert (directory/poster["file"]).read_bytes().startswith(b"\x89PNG\r\n\x1a\n")
    ffprobe = subprocess.run([
        "ffprobe","-v","error","-show_entries","stream=width,height,codec_name:format=duration",
        "-of","json",str(directory/video["file"])
    ],capture_output=True,text=True,timeout=20,check=True)
    output = json.loads(ffprobe.stdout)
    assert output["streams"][0]["codec_name"] == "h264"
    assert (output["streams"][0]["width"], output["streams"][0]["height"]) == (1920,1080)
    assert abs(float(output["format"]["duration"])-video["seconds"]) < 0.15
    print("AGM345_FINISHED_PRODUCT_VERIFIED video=h264 poster=png dimensions=1920x1080"
          " hashes=match fixture_only=true price=unavailable delivery=NOT_SENT")


if __name__ == "__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--product-dir",required=True)
    args=p.parse_args()
    inspect(args.product_dir)
