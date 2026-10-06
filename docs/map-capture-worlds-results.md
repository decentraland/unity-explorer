# Map capture: worlds level 4 results

Results of the first full capture of every Decentraland world, 2026-10-06, written for whoever picks up the worlds
work next. How the worlds mode works and how to run it is in `docs/map-capture-session.md` and
`docs/map-capture-handover.md`; this file is what the run produced and what it cost.

## What was captured

Every world listed by `https://worlds-content-server.decentraland.org/index` (1,671 worlds), rendered from a standalone
build of `MapCapture.unity` on the Windows machine:

```
MapCapture.exe -logFile <LOG> --map-capture-worlds all --map-capture-level 4 --map-capture-tile-px 512 --map-capture-out <OUTPUT_DIR> --map-capture-cache <CACHE_DIR> --map-capture-hour 10 --map-capture-timeout 600
```

- Level 4 of the client's satellite grid: 20x20-parcel tiles at 512 px, tile `i,j` covering X `-152+20i..-133+20i` and
  Y `133-20j..152-20j`, written as `worlds/<world name>/4/<i>,<j>.jpg` plus one `manifest.json` per world.
- Each world covers its whole generated terrain (bounding box of its parcels, the terrain padding and the cliffs), not
  only the tiles its scenes touch. Terrain-only tiles render without loading anything.
- Day only (hour 10). Scenes load their abgen ISS LOD_0; a scene without an ISS descriptor fails fast and is listed in
  the tile's `failedParcels`.
- The tiles were then converted to KTX2 with `scripts/map_worlds_ktx2.py` (ETC1S, 8 workers), the format the client
  streams.

## Results

| | |
|---|---|
| Worlds in the index | 1,671 |
| Complete | 1,667 |
| Failed | 3 (see below) |
| Skipped | 1 (see below) |
| Tiles | 8,504 (mean 5.1 per world, median 2, max 256) |
| Tiles with every scene loaded | 7,755 |
| Tiles with failed parcels | 749 (130,950 parcels), across 205 worlds |
| Tiles with pending parcels (timeouts) | 0 |
| JPEG output | 743 MB (about 87 KB per tile) |
| KTX2 output | 404 MB (about 47 KB per tile) |

Failed worlds, none of which can be captured:

- `aurorahollows.dcl.eth`, `duat.dcl.eth`: the worlds server answers 401, "blocked since 2024-09-16 as it exceeded its
  allowed storage space".
- `shemale.dcl.eth`: 404, no scene deployed.

Skipped: `neverlandranch.dcl.eth`, 232 of its 400 parcels lie outside the satellite grid.

The 749 tiles with failed parcels are scenes abgen has no ISS descriptor for; they render as bare terrain. When abgen
regenerates them, delete those worlds' `manifest.json` and rerun with a list file of just those worlds.

## How long it took

| Step | Duration |
|---|---|
| Capture | 2 h 51 m 41 s, one process, no crashes, hangs or restarts |
| KTX2 conversion | 16 m 55 s |

Boot takes about 18 s. Per world (complete worlds, from the manifests):

| | Mean | Median | p90 | Max |
|---|---|---|---|---|
| Seconds per world | 6.1 | 3.3 | 7.9 | 822 |
| Seconds per tile | 0.86 | 0.50 | 2.3 | 125 |

Only 4,288 tiles took measurable time; terrain-only tiles cost about nothing.

Where the time goes, summed over all worlds:

| Phase | Seconds | Share |
|---|---|---|
| Realm (`/about` and manifest) | 673 | 7% |
| Scene definitions | 2,171 | 21% |
| Terrain | 3 | 0% |
| Tiles (load, render, write) | 7,432 | 73% |

Slowest worlds:

| World | Tiles | Parcels | Scenes | Seconds |
|---|---|---|---|---|
| ontherise.dcl.eth | 256 | 90,000 | 222 | 822 |
| excitedhamsters.dcl.eth | 256 | 90,000 | 144 | 560 |
| bitfiend.dcl.eth | 256 | 90,000 | 221 | 552 |
| swissverse.dcl.eth | 256 | 5,967 | 10 | 414 |
| worldtrack.dcl.eth | 256 | 12,855 | 2 | 188 |
| flandersfields.dcl.eth | 6 | 625 | 1 | 131 |
| xdstandart.dcl.eth | 169 | 2,985 | 26 | 75 |
| bubblebash.dcl.eth | 256 | 4,722 | 3 | 70 |
| swiss.dcl.eth | 256 | 3,215 | 5 | 65 |
| atlasone.dcl.eth | 256 | 2,180 | 6 | 64 |

The three 90,000-parcel worlds are about 19% of the run. Each spends 3 to 4.5 minutes fetching definitions before the
first tile, with no log line in between; that is expected, not a hang.

The asset bundle cache grew by about 12 GB (21 GB to 33 GB). The process peaked at about 4.4 GB of memory.

## Known issues

- The run exited with code 0 although 3 worlds failed and 749 tiles have failed parcels. It should exit 1 so an
  orchestrator can tell; check the exit code logic in `MapCaptureWorldsJob`.
- The log has about 100 "Uploading Crash Report" lines. They are not crashes, only the stack traces of logged
  exceptions: asset bundle manifest fallback 404s and the errors of the failed worlds.

## Estimates for the next passes

Linear extrapolations from this run; run a pilot on the slowest worlds above before committing to a long run.

| Pass | Tiles | Capture | KTX2 output |
|---|---|---|---|
| Night (hour 23), warm bundle cache | 8.5k | 2 to 2.5 h | 404 MB |
| Level 5 (10-parcel tiles) | about 34k | 5 to 8 h | about 1.6 GB |
| Level 6 (5-parcel tiles) | about 136k | 15 to 30 h | about 6.5 GB |

The night pass saves the bundle downloads but not the realm and definition requests (about 47 minutes). Level 6
output does not fit on the system drive of the capture machine; write it to the second disk.
