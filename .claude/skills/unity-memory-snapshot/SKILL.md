---
name: unity-memory-snapshot
description: "Parse Unity Memory Profiler `.snap` capture files headlessly to extract native-object memory grouped by type, and diff/scale memory across captures. Use whenever you need to read a Unity memory dump/snapshot without the Editor, answer 'what's using memory' from a .snap, document a memory baseline, compare two captures, find per-instance (e.g. per-avatar) memory costs, or investigate textures/meshes/materials/animators/render-textures growth. The .snap format is proprietary and was reverse-engineered once — always use the bundled parser instead of re-deriving it."
user-invocable: true
---

# Unity Memory Snapshot Parser

Reads Unity Memory Profiler `.snap` files (the `QueriedSnapshot` binary format) directly, so
you can extract and compare memory without opening the Editor. The format is undocumented and
chunked; the bundled `scripts/parse_snapshot.py` encodes it (reverse-engineered from the
`com.unity.memoryprofiler` package source and validated against real captures). **Never
re-reverse-engineer the format — call the script.**

`.snap` files normally live in `<project>/MemoryCaptures/`.

## What it reports

`NativeObjects_Size` grouped by `NativeTypes_Name` — i.e. the same per-type "allocated size" the
Memory Profiler's *Unity Objects* table shows (Texture2D, Texture2DArray, RenderTexture, Mesh,
Material, Animator, Transform, etc.), plus object counts and totals.

Important scope limits (state these when reporting, so numbers aren't over-claimed):
- These are **native object own-sizes**. They exclude exclusively-owned child allocations (e.g. an
  Animator's PlayableGraph), the managed/GC heap, and graphics-resource accounting
  (`NativeGfxResourceReferences`) — so `ComputeBuffer`/`GraphicsBuffer` (skinning buffers, the GVB)
  and some texture GPU memory are **not** in this view.
- The Memory Profiler's grand total (which includes managed + graphics) will be larger than the
  "total native object size" reported here.

## Usage

Run with the project's Python (3.x). Quote paths containing `#` or spaces.

```bash
# Per-type breakdown of one capture (top 30 by size)
python .claude/skills/unity-memory-snapshot/scripts/parse_snapshot.py summary "Explorer/MemoryCaptures/100_Avatars.snap"

# JSON output (for further processing / documenting baselines)
python .../parse_snapshot.py summary path/to.snap --json --top 50

# Delta between two captures (A - B), optionally per-N (e.g. per avatar)
python .../parse_snapshot.py diff "100_Avatars.snap" "No_Avatars.snap" --per 100

# Category x capture matrix across many files (linear-per-instance vs shared/flat)
python .../parse_snapshot.py scale No=No_Avatars.snap 100=100_Avatars.snap 500=500_Avatars.snap \
    --categories Texture2DArray,Animator,Mesh,Material,Transform
```

## How to use the results

- **Baseline / "what's using memory":** run `summary`. The top types are your memory budget.
- **Find per-instance cost (per avatar, per NPC, per scene object):** capture with N and with 0 of
  the thing, then `diff A B --per N`. A clean diff shows *exactly* N of each per-instance object
  (e.g. +N Animators) — that self-validates the comparison; call it out.
- **Distinguish a real per-instance cost from a shared/slab cost:** run `scale` across several
  counts. Linear growth = per-instance (optimization target); flat = shared (leave it).
- **Caveat to watch:** two captures from different sessions can have different base scenes loaded.
  Trust categories whose delta scales cleanly with the instance count; be skeptical of one-off jumps
  in otherwise-flat categories (often capture artifacts like profiler RenderTextures).

## Extending

The parser exposes a `Snapshot` class. `native_objects_by_type()` is the main entry point;
`type_names()`, `const_array_ints(entry_type)`, and `dynamic_array_bytes(entry_type)` give raw
access to any `EntryType` (ordinals listed at the top of the script; full enum in the package's
`EntryType.cs`). To pull object names, instance IDs, connections, or graphics-resource sizes, read
the corresponding entries via those primitives rather than rewriting the file reader.
