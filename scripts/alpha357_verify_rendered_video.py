#!/usr/bin/env python3
"""Read-only ALPHA-357 actual-render output QA. Requires local ffmpeg/ffprobe.

Checks exact 2/4/6 pixel partitions, three decoded frame samples, expected
fictional ad-panel color coverage and movement in the single creator center.
No video creation, external API, commercial authorization or production claim.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
from statistics import median

W, H = 1920, 1080
GEOMETRY = {
    2: {"slots": [("BOTTOM_BLUE", 0, 810, 960, 270, "blue"),
                   ("BOTTOM_ORANGE", 960, 810, 960, 270, "orange")],
        "creator": (0, 0, 1920, 810)},
    4: {"slots": [("TOP_BLUE", 0, 0, 960, 180, "blue"),
                   ("TOP_ORANGE", 960, 0, 960, 180, "orange"),
                   ("BOTTOM_GREEN", 0, 900, 960, 180, "green"),
                   ("BOTTOM_PURPLE", 960, 900, 960, 180, "purple")],
        "creator": (0, 180, 1920, 720)},
    6: {"slots": [("TOP_BLUE", 0, 0, 960, 180, "blue"),
                   ("TOP_ORANGE", 960, 0, 960, 180, "orange"),
                   ("LEFT_PURPLE", 0, 180, 270, 720, "purple"),
                   ("RIGHT_GREEN", 1650, 180, 270, 720, "green"),
                   ("BOTTOM_BLUE", 0, 900, 960, 180, "blue"),
                   ("BOTTOM_PURPLE", 960, 900, 960, 180, "purple")],
        "creator": (270, 180, 1380, 720)}
}


def geometry_partition(layout):
    """Every scanline must have exactly one region, without inferred pixels."""
    if layout not in GEOMETRY:
        raise ValueError("Only the existing 2/4/6 Composer layouts are supported")
    entry = GEOMETRY[layout]
    regions = [(*s[1:5], s[0]) for s in entry["slots"]] + [(*entry["creator"], "CREATOR")]
    for x, y, width, height, name in regions:
        if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > W or y + height > H:
            raise ValueError(f"Out-of-canvas rectangle: {name}")
    for row in range(H):
        intervals = sorted((x, x + width, name) for x, y, width, height, name in regions if y <= row < y + height)
        previous = 0
        for left, right, name in intervals:
            if left != previous:
                defect = "overlap" if left < previous else "gap"
                raise ValueError(f"{defect} at row={row}, x={left}, region={name}, expected={previous}")
            previous = right
        if previous != W:
            raise ValueError(f"gap at row={row}: coverage ends at {previous}")
    return {"canvas": [W, H], "rectangles": layout,
            "creator_safe": list(entry["creator"]), "gap_px": 0, "overlap_px": 0,
            "out_of_bounds": 0, "creator_intrusion_px": 0,
            "slots": [{"id": name, "x": x, "y": y, "width": width, "height": height}
                      for name, x, y, width, height, _ in entry["slots"]]}


def probe_video(video):
    result = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0",
                             "-show_entries", "stream=width,height,codec_name:format=duration",
                             "-of", "json", str(video)], capture_output=True, text=True, check=True)
    data = json.loads(result.stdout)
    if not data.get("streams"):
        raise ValueError("No decodable video stream")
    stream = data["streams"][0]
    if (stream.get("width"), stream.get("height")) != (W, H):
        raise ValueError(f"Expected {W}x{H}, got {stream.get('width')}x{stream.get('height')}")
    seconds = float(data["format"]["duration"])
    if seconds < 0.8:
        raise ValueError("Video too short for three-frame QA")
    return stream, seconds


def decode_rgb(video, seconds):
    result = subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-ss", str(seconds),
                             "-i", str(video), "-frames:v", "1", "-f", "rawvideo",
                             "-pix_fmt", "rgb24", "pipe:1"], capture_output=True, check=True)
    if len(result.stdout) != W * H * 3:
        raise ValueError(f"Incomplete decoded frame at t={seconds}")
    return result.stdout


def sampled_rgb(frame, x, y, radius=6):
    if x < radius or y < radius or x >= W-radius or y >= H-radius:
        raise ValueError("Pixel sample outside canvas")
    values = [[], [], []]
    for row in range(y-radius, y+radius+1, 2):
        for col in range(x-radius, x+radius+1, 2):
            start = (row * W + col)*3
            for channel in range(3):
                values[channel].append(frame[start+channel])
    return tuple(round(median(channel), 2) for channel in values)


def matches_hue(rgb, color):
    r, g, b = rgb
    return {
        "blue": b > g * 1.45 and g > r * 1.5,
        "orange": r > g * 1.45 and g > b * 1.55,
        "green": g > r * 1.5 and g > b * 1.3,
        "purple": b > g * 2 and r > g * 1.45,
    }[color]


def panel_samples(layout):
    points = []
    for name, x, y, width, height, hue in GEOMETRY[layout]["slots"]:
        if width < 350:
            for dy in (25, height//2+110, height-30):
                points.append((name, x + width//2, y+dy, hue))
        else:
            points.append((name, x+width//2, y+50, hue))
    return points


def motion_score(first, second):
    total = count = 0
    for y in range(495, 660, 13):
        for x in range(680, 1220, 13):
            start = (y*W+x)*3
            for channel in range(3):
                total += abs(first[start+channel] - second[start+channel])
                count += 1
    return round(total/count, 3)


def verify(video, layout):
    video = Path(video)
    if not video.is_file():
        raise FileNotFoundError(video)
    if not shutil.which("ffmpeg") or not shutil.which("ffprobe"):
        raise RuntimeError("ffmpeg and ffprobe are required")
    geometry = geometry_partition(layout)
    stream, duration = probe_video(video)
    times = [round(duration * fraction, 3) for fraction in (0.18, 0.46, 0.73)]
    frames = [decode_rgb(video, time) for time in times]
    measured = []
    for frame_idx, frame in enumerate(frames):
        for name, x, y, hue in panel_samples(layout):
            rgb = sampled_rgb(frame, x, y)
            passed = matches_hue(rgb, hue)
            measured.append({"slot": name, "seconds": times[frame_idx], "xy": [x,y],
                             "expected_hue": hue, "median_rgb": list(rgb), "pass": passed})
            if not passed:
                raise AssertionError(f"Panel appears absent/miscolored: {name} at {x},{y} = {rgb}")
    motion = [motion_score(frames[0], frame) for frame in frames[1:]]
    if min(motion) <= 1.0:
        raise AssertionError(f"Central podcast motion not demonstrated: {motion}")
    return {"directive_id": "ALPHA-VIDEO-GEOMETRY-QUALITY-357-M2M",
            "status": "OFFLINE_SYNTHETIC_VIDEO_SAMPLE_PASS", "layout": layout,
            "source_video_comparison": "NOT_PERFORMED", "real_creator_footage_approved": False,
            "production_renderer_integrated": False, "filename": video.name,
            "sha256": hashlib.sha256(video.read_bytes()).hexdigest(),
            "codec": stream["codec_name"], "duration_seconds": duration,
            "geometry": geometry, "sample_times_seconds": times,
            "panel_pixel_sample_count": len(measured), "panel_samples": measured,
            "creator_motion_mean_absolute_error": motion,
            "limitations": "Only synthetic fixture hue/geometry and center motion; no blanket text/logo, rights, production, or source-video comparison claims."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--layout", type=int, choices=[2,4,6], required=True)
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    try:
        result = verify(args.video, args.layout)
    except (ValueError, AssertionError, RuntimeError, FileNotFoundError, subprocess.CalledProcessError) as error:
        print(f"ALPHA357 FAIL: {error}", file=sys.stderr)
        return 1
    if args.report:
        args.report.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"ALPHA357 PASS layout={args.layout} pixels={W}x{H} samples={result['panel_pixel_sample_count']} motion={result['creator_motion_mean_absolute_error']} sha256={result['sha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
