"""
Builds the satellite map zoom pyramid from a region capture of the map capture tool.

Input is a capture folder written without --map-capture-client-map: square blocks named
{minX}_{minY}.jpg (or .png) plus manifest.json. Output is <out>/<level>/<i>,<j>.jpg on the client's
satellite grid: 320x320 parcels starting at parcel (-152, -167), level L split into 2^L x 2^L tiles,
i counting eastward and j counting southward. Level 3 is the minimap's 8x8 grid of 40-parcel chunks.

The finest level is cut from the blocks; every coarser level halves four tiles of the level above.
Tiles outside the captured region are filled with the background colour.

Needs Python 3 and Pillow.

Usage: python map_pyramid.py CAPTURE_DIR OUT_DIR [--max-level 8] [--min-level 1] [--tile 512]
                             [--quality 90] [--workers 4]
"""
import argparse
import json
import os
from concurrent.futures import ProcessPoolExecutor

from PIL import Image

GRID_MIN_X = -152
GRID_MIN_Y = -167
GRID_PARCELS = 320

# Finest-level tiles are cut per 40x40-parcel group: one client chunk, a whole number of capture
# blocks and of finest tiles, so only one group's blocks are decoded at a time.
GROUP_PARCELS = 40
BACKGROUND = (30, 30, 30)


def block_path(capture_dir, min_x, min_y):
    for ext in ("jpg", "png"):
        path = os.path.join(capture_dir, f"{min_x}_{min_y}.{ext}")
        if os.path.exists(path):
            return path
    return None


def cut_group(job):
    """Cuts every finest-level tile of one 40x40-parcel group out of the blocks that cover it."""
    capture_dir, out_dir, level, tile_px, quality, block, ppp, gi, gj = job
    tiles_per_group = (2 ** level) * GROUP_PARCELS // GRID_PARCELS
    tile_src = GROUP_PARCELS * ppp // tiles_per_group
    group_src = GROUP_PARCELS * ppp
    block_src = block * ppp

    group_min_x = GRID_MIN_X + gi * GROUP_PARCELS
    group_max_y = GRID_MIN_Y + GRID_PARCELS - gj * GROUP_PARCELS
    blocks_per_group = GROUP_PARCELS // block

    # Blocks are north-up: the image's top row is the block's northern edge.
    canvas = Image.new("RGB", (group_src, group_src), BACKGROUND)
    found = 0

    for bj in range(blocks_per_group):
        for bi in range(blocks_per_group):
            path = block_path(capture_dir, group_min_x + bi * block, group_max_y - (bj + 1) * block)
            if path is None:
                continue
            with Image.open(path) as image:
                canvas.paste(image.convert("RGB"), (bi * block_src, bj * block_src))
            found += 1

    if found == 0:
        return 0

    level_dir = os.path.join(out_dir, str(level))
    for tj in range(tiles_per_group):
        for ti in range(tiles_per_group):
            box = (ti * tile_src, tj * tile_src, (ti + 1) * tile_src, (tj + 1) * tile_src)
            tile = canvas.crop(box).resize((tile_px, tile_px), Image.LANCZOS)
            i = gi * tiles_per_group + ti
            j = gj * tiles_per_group + tj
            tile.save(os.path.join(level_dir, f"{i},{j}.jpg"), quality=quality)

    return tiles_per_group * tiles_per_group


def halve_tile(job):
    """Builds one tile from the four tiles under it on the level above."""
    out_dir, level, tile_px, quality, i, j = job
    finer_dir = os.path.join(out_dir, str(level + 1))
    canvas = Image.new("RGB", (tile_px * 2, tile_px * 2), BACKGROUND)
    found = 0

    for dj in range(2):
        for di in range(2):
            path = os.path.join(finer_dir, f"{2 * i + di},{2 * j + dj}.jpg")
            if not os.path.exists(path):
                continue
            with Image.open(path) as child:
                canvas.paste(child, (di * tile_px, dj * tile_px))
            found += 1

    if found == 0:
        return 0

    canvas.resize((tile_px, tile_px), Image.LANCZOS).save(os.path.join(out_dir, str(level), f"{i},{j}.jpg"), quality=quality)
    return 1


def main():
    parser = argparse.ArgumentParser(description="Build the satellite map zoom pyramid from a region capture.")
    parser.add_argument("capture_dir")
    parser.add_argument("out_dir")
    parser.add_argument("--max-level", type=int, default=8)
    parser.add_argument("--min-level", type=int, default=1)
    parser.add_argument("--tile", type=int, default=512, help="tile side in pixels")
    parser.add_argument("--quality", type=int, default=90, help="JPEG quality of the tiles")
    parser.add_argument("--workers", type=int, default=4)
    args = parser.parse_args()

    with open(os.path.join(args.capture_dir, "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)

    block = manifest["blockSize"]
    ppp = manifest["pixelsPerParcel"]
    groups = GRID_PARCELS // GROUP_PARCELS

    if manifest.get("clientMap"):
        raise SystemExit("This is a client-map capture; the pyramid needs a region capture.")
    if GROUP_PARCELS % block != 0:
        raise SystemExit(f"Block size {block} must divide {GROUP_PARCELS}.")
    if not 3 <= args.max_level or (GROUP_PARCELS * ppp) % ((2 ** args.max_level) // groups) != 0:
        raise SystemExit(f"Level {args.max_level} does not split {GROUP_PARCELS} parcels at {ppp} px/parcel into whole pixels.")

    origin = manifest["gridOrigin"]
    if (origin["x"] - GRID_MIN_X) % block or (origin["y"] - GRID_MIN_Y) % block:
        raise SystemExit(f"Capture grid origin {origin['x']},{origin['y']} is not aligned with the client grid in {block}-parcel blocks.")

    finest_px = GRID_PARCELS * ppp // (2 ** args.max_level)
    if finest_px < args.tile:
        print(f"Warning: level {args.max_level} upsamples {finest_px} px of capture to {args.tile} px tiles.")

    for level in range(args.min_level, args.max_level + 1):
        os.makedirs(os.path.join(args.out_dir, str(level)), exist_ok=True)

    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        jobs = [(args.capture_dir, args.out_dir, args.max_level, args.tile, args.quality, block, ppp, gi, gj)
                for gj in range(groups) for gi in range(groups)]
        written = sum(pool.map(cut_group, jobs))
        print(f"Level {args.max_level}: {written} tiles")

        for level in range(args.max_level - 1, args.min_level - 1, -1):
            side = 2 ** level
            jobs = [(args.out_dir, level, args.tile, args.quality, i, j) for j in range(side) for i in range(side)]
            written = sum(pool.map(halve_tile, jobs, chunksize=64))
            print(f"Level {level}: {written} tiles")


if __name__ == "__main__":
    main()
