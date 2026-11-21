#!/usr/bin/env python3
"""ALPHA-345 reusable local creative factory: real MP4 + PNG + receipt.

The fictional ARE-P01 demo is NOT an authorized customer advertisement.
Requires existing FFmpeg, FFprobe and DejaVu font; no API, account, tokens,
database, image credits, external network, bookings or paid creative model.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

SIZE = (1920, 1080)
PROTECTED_CENTER = (320, 180, 1280, 720)
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
SLOTS = ("LEFT_VERTICAL", "BOTTOM_FULL")


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def call(args, timeout=120):
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True,
                            text=True, timeout=timeout, check=False)
    if result.returncode:
        raise RuntimeError("Local media tool rejected the creative: " + result.stderr[-1500:])
    return result.stdout


def overlaps(a, b):
    x, y, w, h = a
    u, v, m, n = b
    return x < u + m and u < x + w and y < v + n and v < y + h


def geometry(path):
    with open(path, encoding="utf-8-sig", newline="") as handle:
        rows = {row["slotId"]: row for row in csv.DictReader(handle, delimiter="\t")}
    if not all(name in rows for name in SLOTS):
        raise ValueError("Existing ARE-P01 owner geometry is incomplete")
    version, regions = rows[SLOTS[0]]["version"], {}
    for name in SLOTS:
        row = rows[name]
        if row["version"] != version or (int(row["canvasWidth"]), int(row["canvasHeight"])) != SIZE:
            raise ValueError("Source dimensions/geometry version disagreement")
        if not row["approvedOn"] or not row["approvingAuthority"]:
            raise ValueError("Owner geometry lacks recorded provenance")
        rect = tuple(int(row[key]) for key in ("originX", "originY", "width", "height"))
        x, y, w, h = rect
        if (x < 0 or y < 0 or w <= 0 or h <= 0 or x+w > SIZE[0] or
            y+h > SIZE[1] or w*h != int(row["area"]) or
            overlaps(rect, PROTECTED_CENTER) or
            any(overlaps(rect, other) for other in regions.values())):
            raise ValueError("Sponsor layout overlaps protected creator content or invalid bounds")
        regions[name] = rect
    # Validate this particular deterministic renderer: no inferred/scaled geometry.
    if regions["LEFT_VERTICAL"] != (0, 0, 320, 1080) or regions["BOTTOM_FULL"] != (320, 900, 1280, 180):
        raise ValueError("This version of the template needs an explicit owner-approved layout revision")
    return version, regions


def recipe(path):
    r = json.loads(Path(path).read_text(encoding="utf-8"))
    if r.get("product_id") != "ARE-P01" or r.get("seconds") not in (6, 8, 10) or r.get("fps") not in (12, 15):
        raise ValueError("Unsupported catalog product or movie format")
    fields = {
        "brand_line_one": 14, "brand_line_two": 14, "brand_caption": 28,
        "headline": 38, "creator_title": 22, "creator_subtitle": 56,
        "disclaimer": 72
    }
    for key, length in fields.items():
        value = r.get(key)
        if not isinstance(value, str) or not value.strip() or len(value) > length or "\n" in value:
            raise ValueError("Invalid reusable creative text: " + key)
    for key in ("accent_hex", "brand_bg_hex"):
        if not re.fullmatch("[A-Fa-f0-9]{6}", r.get(key, "")):
            raise ValueError("Invalid creative color")
    if not re.fullmatch("[A-Z0-9-]{8,80}", r.get("concept_id", "")):
        raise ValueError("Invalid synthetic concept id")
    if "FICTIONAL" not in r["disclaimer"].upper() or "NOT A LIVE" not in r["disclaimer"].upper():
        raise ValueError("Fictional creative disclosure cannot be omitted")
    return r


def produce(output_dir, recipe_file, geometry_file):
    if not shutil.which("ffmpeg") or not shutil.which("ffprobe") or not Path(FONT).is_file():
        raise RuntimeError("Local FFmpeg, FFprobe and DejaVu Bold font are required")
    r = recipe(recipe_file)
    version, regions = geometry(geometry_file)
    out = Path(output_dir).resolve()
    out.mkdir(parents=True, exist_ok=True)
    video, poster, receipt = (out/"orchid-roast-creator-overlay.mp4",
                              out/"orchid-roast-ad-poster.png", out/"product-receipt.json")
    if any(p.exists() for p in (video, poster, receipt)):
        raise FileExistsError("Refusing to overwrite a previously generated product")

    with tempfile.TemporaryDirectory(prefix="agm345-product-") as tmp:
        fields = {
            "brand_top":r["brand_line_one"], "brand_bottom":r["brand_line_two"],
            "brand_caption":r["brand_caption"], "headline":r["headline"],
            "creator_title":r["creator_title"], "creator_sub":r["creator_subtitle"],
            "left_label":"FICTIONAL BRAND", "sponsor_label":"SPONSOR CREATIVE / DEMO",
            "creator_label":"SIMULATED CREATOR CONTENT",
            "small":"ORIGINAL CREATOR PLACEHOLDER",
            "disclaimer":r["disclaimer"],
            "frame_mark":"ALPHA GLOBAL MEDIA / CREATIVE FACTORY",
            "package_mark":"OR",
        }
        files = {}
        for name, value in fields.items():
            namepath = Path(tmp)/(name+".txt")
            namepath.write_text(value, encoding="utf-8")
            files[name] = str(namepath)
        gold, green = r["accent_hex"], r["brand_bg_hex"]
        shapes = [
            "format=yuv420p",
            "drawbox=x=320:y=0:w=1280:h=180:color=0x0D3739:t=fill",
            "drawbox=x=320:y=180:w=1280:h=720:color=0x123032:t=fill",
            "drawbox=x=1600:y=0:w=320:h=1080:color=0x081A20:t=fill",
            f"drawbox=x=0:y=0:w=320:h=1080:color=0x{green}:t=fill",
            f"drawbox=x=320:y=900:w=1280:h=180:color=0x{green}:t=fill",
            f"drawbox=x=306:y=0:w=14:h=1080:color=0x{gold}:t=fill",
            f"drawbox=x=320:y=900:w=1280:h=8:color=0x{gold}:t=fill",
            "drawbox=x=390:y=250:w=1140:h=580:color=0x1C4443:t=fill",
            "drawbox=x=418:y=278:w=1084:h=524:color=0x153837:t=fill",
            "drawbox=x=442:y=302:w=1036:h=476:color=0x103032:t=fill",
            f"drawbox=x=442:y=302:w=1036:h=6:color=0x{gold}:t=fill",
            "drawbox=x=0:y=150:w=300:h=3:color=0x4C8173:t=fill",
            "drawbox=x=100:y=492:w=139:h=224:color=0xBF9F66:t=fill",
            "drawbox=x=109:y=503:w=121:h=201:color=0xE8D6A9:t=fill",
            "drawbox=x=117:y=513:w=105:h=15:color=0xCBB27B:t=fill",
            "drawbox=x=121:y=548:w=97:h=113:color=0x153C37:t=fill",
            "drawbox=x=129:y=557:w=81:h=96:color=0x1D4B43:t=fill",
            f"drawbox=x=135:y=649:w=70:h=3:color=0x{gold}:t=fill",
            "drawbox=x=48:y=740:w=224:h=3:color=0x4C8173:t=fill",
            f"drawbox=x=355:y=985:w=60:h=5:color=0x{gold}:t=fill",
            "drawbox=x=1620:y=100:w=2:h=800:color=0x315251:t=fill",
        ]
        def label(field, x, y, size, color="white"):
            return (f"drawtext=fontfile={FONT}:textfile={files[field]}:"
                    f"fontsize={size}:fontcolor={color}:x={x}:y={y}")
        drawings = [
            label("brand_top",35,235,56,f"0x{gold}"),
            label("brand_bottom",35,305,55,"0xF7F0E5"),
            label("brand_caption",36,400,18,"0xD6E8DC"),
            label("package_mark",142,575,48,f"0x{gold}"),
            label("left_label",38,790,18,"0xB5D9CC"),
            label("creator_label",450,330,24,f"0x{gold}"),
            label("creator_title","480+12*sin(t*0.9)",445,79,"0xF4EEE0"),
            label("creator_sub",482,570,32,"0xB5D1CA"),
            label("small",480,718,19,"0x78A69D"),
            label("sponsor_label",365,929,16,"0xD2E8DB"),
            label("headline",437,952,47,"0xF9F3EA"),
            label("disclaimer",438,1023,16,"0xB5D2C7"),
            label("frame_mark",356,52,28,"0xC7E6D7"),
        ]
        filt = ",".join(shapes+drawings)
        sec, fps = r["seconds"], r["fps"]
        try:
            call(["ffmpeg","-hide_banner","-loglevel","error","-nostdin","-y",
                  "-f","lavfi","-i",f"color=c=0x071D20:s=1920x1080:r={fps}:d={sec}",
                  "-vf",filt,"-an","-c:v","libx264","-preset","ultrafast",
                  "-crf","23","-pix_fmt","yuv420p","-movflags","+faststart",
                  "-t",str(sec),str(video)])
            call(["ffmpeg","-hide_banner","-loglevel","error","-nostdin","-y",
                  "-ss","1","-i",str(video),"-frames:v","1",str(poster)])
            meta = json.loads(call(["ffprobe","-v","error","-show_entries",
                                    "stream=width,height,codec_name:format=duration",
                                    "-of","json",str(video)]))
            if (meta["streams"][0]["width"],meta["streams"][0]["height"]) != SIZE:
                raise RuntimeError("Media frame dimensions diverge from existing owner geometry")
        except Exception:
            video.unlink(missing_ok=True)
            poster.unlink(missing_ok=True)
            raise

    receipt_data = {
        "schema":"ALPHA-345-OFFLINE-FINISHED-AD-PRODUCT/V1",
        "mode":"OFFLINE_SYNTHETIC_ONLY",
        "product_id":"ARE-P01",
        "concept_id":r["concept_id"],
        "brand":r["brand_line_one"]+" "+r["brand_line_two"],
        "geometry_version":version,
        "sponsor_slots":{k:dict(zip(("x","y","width","height"),v)) for k,v in regions.items()},
        "protected_creator_area":dict(zip(("x","y","width","height"),PROTECTED_CENTER)),
        "media":{
            "video":{"file":video.name,"mime":"video/mp4","seconds":sec,
                     "width":SIZE[0],"height":SIZE[1],"fps":fps,
                     "sha256":sha256(video),"bytes":video.stat().st_size},
            "poster":{"file":poster.name,"mime":"image/png",
                      "width":SIZE[0],"height":SIZE[1],
                      "sha256":sha256(poster),"bytes":poster.stat().st_size}
        },
        "commercial_approval":False,
        "creator_advertiser_rights_verified":False,
        "quote_status":"PRICING_UNAVAILABLE",
        "can_state_price":False,
        "reservation_authorized":False,
        "payment_authorized":False,
        "real_impressions":0,
        "delivery_status":"NOT_SENT",
        "notice":r["disclaimer"]
    }
    receipt.write_text(json.dumps(receipt_data,sort_keys=True,indent=2),encoding="utf-8")
    print("AGM345_PRODUCT_CREATED product=ARE-P01 video="+str(video)+
          " poster="+str(poster)+" demo_only=true delivery=NOT_SENT")
    return receipt_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir",required=True)
    parser.add_argument("--recipe",default="tools/AGM345.ProductFactory/templates/orchid-roast.json")
    parser.add_argument("--geometry-tsv",default="assets/alpha-prototypes/OWNER-GEOMETRY.tsv")
    args = parser.parse_args()
    produce(args.output_dir,args.recipe,args.geometry_tsv)


if __name__ == "__main__":
    main()
