"""
Converts the world tiles written by the map capture's worlds mode (--map-capture-worlds) from JPEG to KTX2.

Input is the capture's output folder: <capture>/worlds/<worldName>/<level>/<i>,<j>.jpg. Output mirrors it as
<out>/worlds/<worldName>/<level>/<i>,<j>.ktx2, the layout the client fetches from
{satelliteUrl}/worlds/{worldName}/{level}/{i},{j}.ktx2. Encoding is the one map_pyramid_ktx2.py uses for Genesis City
(ETC1S by default, --uastc for UASTC), sRGB, no mips, stored bottom row first because the client loads KTX2 without
applying its orientation.

Only worlds whose manifest says "complete" are converted, so a world still being captured is not published half done;
--include-incomplete converts whatever tiles exist. Already converted tiles are skipped, so an interrupted run can be
resumed.

Needs Python 3 and toktx from KTX-Software 4.x (https://github.com/KhronosGroup/KTX-Software/releases).
Serve the tiles with Content-Type: image/ktx2.

Usage: python map_worlds_ktx2.py CAPTURE_DIR OUT_DIR [--toktx toktx] [--uastc] [--workers 4]
                                 [--level 4] [--include-incomplete]
"""
import argparse
import json
import os
from concurrent.futures import ThreadPoolExecutor

from map_pyramid_ktx2 import ETC1S_ARGS, UASTC_ARGS, convert_tile

WORLDS_FOLDER = "worlds"
MANIFEST_FILE = "manifest.json"


def world_status(world_dir):
    try:
        with open(os.path.join(world_dir, MANIFEST_FILE), encoding="utf-8") as manifest:
            return json.load(manifest).get("status")
    except (OSError, ValueError):
        return None


def main():
    parser = argparse.ArgumentParser(description="Convert captured world tiles from JPEG to KTX2.")
    parser.add_argument("capture_dir", help="the capture's output folder, holding worlds/")
    parser.add_argument("out_dir", help="folder that receives worlds/<name>/<level>/<i>,<j>.ktx2")
    parser.add_argument("--toktx", default="toktx", help="path to the toktx executable")
    parser.add_argument("--uastc", action="store_true", help="encode UASTC instead of ETC1S")
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--level", help="convert only this level (default: every level present)")
    parser.add_argument("--include-incomplete", action="store_true", help="also convert worlds whose capture is not complete")
    args = parser.parse_args()

    encode_args = UASTC_ARGS if args.uastc else ETC1S_ARGS
    worlds_dir = os.path.join(args.capture_dir, WORLDS_FOLDER)
    totals = {"worlds": 0, "skipped": 0, "converted": 0, "present": 0}

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for name in sorted(os.listdir(worlds_dir)):
            world_dir = os.path.join(worlds_dir, name)

            if not os.path.isdir(world_dir):
                continue

            status = world_status(world_dir)

            if status != "complete" and not args.include_incomplete:
                if status != "skipped":
                    print(f"{name}: not converted, capture status {status}", flush=True)
                totals["skipped"] += 1
                continue

            levels = [args.level] if args.level else [entry for entry in os.listdir(world_dir) if entry.isdigit()]
            totals["worlds"] += 1

            for level in levels:
                source_dir = os.path.join(world_dir, level)

                if not os.path.isdir(source_dir):
                    continue

                target_dir = os.path.join(args.out_dir, WORLDS_FOLDER, name, level)
                os.makedirs(target_dir, exist_ok=True)

                jobs = [(args.toktx, encode_args, os.path.join(source_dir, tile), os.path.join(target_dir, tile[:-len(".jpg")] + ".ktx2"))
                        for tile in os.listdir(source_dir) if tile.endswith(".jpg")]
                written = sum(pool.map(convert_tile, jobs))
                totals["converted"] += written
                totals["present"] += len(jobs) - written
                print(f"{name} level {level}: {written} tiles converted, {len(jobs) - written} already present", flush=True)

    print(f"Done: {totals['worlds']} worlds, {totals['converted']} tiles converted, {totals['present']} already present, "
          f"{totals['skipped']} worlds not converted", flush=True)


if __name__ == "__main__":
    main()
