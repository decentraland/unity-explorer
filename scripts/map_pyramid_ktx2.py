"""
Converts a satellite map zoom pyramid written by map_pyramid.py from JPEG to KTX2 tiles.

Output mirrors the input layout as <out>/<level>/<i>,<j>.ktx2. Tiles are Basis Universal ETC1S
(about the size of the JPEGs; --uastc trades ~3x the size for near-lossless detail), sRGB, no mips.
They are stored bottom row first because the client loads KTX2 without applying its orientation.

Already converted tiles are skipped, so an interrupted run can be resumed.

Needs Python 3 and toktx from KTX-Software 4.x (https://github.com/KhronosGroup/KTX-Software/releases).
Serve the tiles with Content-Type: image/ktx2; the client picks the KTX2 decoder from that header.

Usage: python map_pyramid_ktx2.py PYRAMID_DIR OUT_DIR [--toktx toktx] [--uastc] [--workers 4]
"""
import argparse
import os
import subprocess
from concurrent.futures import ThreadPoolExecutor

ETC1S_ARGS = ["--encode", "etc1s", "--clevel", "2", "--qlevel", "255"]
UASTC_ARGS = ["--encode", "uastc", "--uastc_quality", "2", "--uastc_rdo_l", "1", "--zcmp", "19"]


def convert_tile(job):
    toktx, encode_args, source, target = job
    if os.path.exists(target):
        return 0

    # toktx writes to a temporary name that is only moved into place once complete.
    partial = target + ".part.ktx2"
    subprocess.run([toktx, "--t2", *encode_args, "--threads", "1", "--lower_left_maps_to_s0t0", "--assign_oetf", "srgb", partial, source],
                   check=True, capture_output=True)
    os.replace(partial, target)
    return 1


def main():
    parser = argparse.ArgumentParser(description="Convert a satellite map zoom pyramid from JPEG to KTX2.")
    parser.add_argument("pyramid_dir")
    parser.add_argument("out_dir")
    parser.add_argument("--toktx", default="toktx", help="path to the toktx executable")
    parser.add_argument("--uastc", action="store_true", help="encode UASTC instead of ETC1S")
    parser.add_argument("--workers", type=int, default=4)
    args = parser.parse_args()

    encode_args = UASTC_ARGS if args.uastc else ETC1S_ARGS
    levels = sorted((name for name in os.listdir(args.pyramid_dir) if name.isdigit()), key=int)

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for level in levels:
            source_dir = os.path.join(args.pyramid_dir, level)
            target_dir = os.path.join(args.out_dir, level)
            os.makedirs(target_dir, exist_ok=True)

            jobs = [(args.toktx, encode_args, os.path.join(source_dir, name), os.path.join(target_dir, name[:-len(".jpg")] + ".ktx2"))
                    for name in os.listdir(source_dir) if name.endswith(".jpg")]
            written = sum(pool.map(convert_tile, jobs))
            print(f"Level {level}: {written} tiles converted, {len(jobs) - written} already present", flush=True)


if __name__ == "__main__":
    main()
