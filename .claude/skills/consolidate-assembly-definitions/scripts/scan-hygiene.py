#!/usr/bin/env python3
"""Assembly folder hygiene scan. Reports:
1. REDUNDANT asmrefs — the nearest ancestor asmdef/asmref already provides the same
   assembly (delete the asmref; the folder is covered anyway).
2. Assembly folders with NO source below (excluding nested assemblies) — delete the
   folder entirely (asmref-only folders are pointless; a code-less asmdef anchor
   should be relocated into one of its member folders).

Usage: python3 scan-hygiene.py [--assets <path-to-Explorer/Assets>]
Exits 1 if anything is found (CI-friendly).
"""
import os, json, io, sys

def find_assets():
    if "--assets" in sys.argv:
        return sys.argv[sys.argv.index("--assets") + 1]
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.normpath(os.path.join(here, "..", "..", "..", "..", "Explorer", "Assets"))

ASSETS = find_assets()

def read_json(p):
    with io.open(p, "r", encoding="utf-8-sig") as f:
        return json.load(f)

# guid -> assembly name, from asmdef metas
guidmap = {}
for root, _d, files in os.walk(os.path.join(ASSETS, "..")):
    for fn in files:
        if fn.endswith(".asmdef"):
            p = os.path.join(root, fn)
            try:
                name = read_json(p)["name"]
                with io.open(p + ".meta", encoding="utf-8-sig") as f:
                    for line in f:
                        if line.startswith("guid:"):
                            guidmap[line.split(":", 1)[1].strip()] = name
                            break
            except (OSError, ValueError, KeyError):
                pass

defs = {}
for root, _d, files in os.walk(ASSETS):
    for fn in files:
        if fn.endswith(".asmdef"):
            defs[root] = ("asmdef", read_json(os.path.join(root, fn))["name"], fn)
        elif fn.endswith(".asmref"):
            ref = read_json(os.path.join(root, fn))["reference"]
            name = guidmap.get(ref[5:], ref) if ref.startswith("GUID:") else ref
            defs[root] = ("asmref", name, fn)

def governing(d):
    p = os.path.dirname(d)
    while len(p) >= len(ASSETS):
        if p in defs:
            return defs[p]
        p = os.path.dirname(p)
    return None

found = 0
print("--- REDUNDANT asmrefs (ancestor already provides same assembly):")
for d in sorted(defs):
    kind, name, fn = defs[d]
    if kind != "asmref":
        continue
    g = governing(d)
    if g and g[1] == name:
        print(f"  {os.path.relpath(d, ASSETS)}\\{fn}  -> {name} (ancestor {g[0]} -> {g[1]})")
        found += 1

print("--- assembly folders with NO source below (excluding nested assemblies):")
CODE_EXT = (".cs", ".jslib", ".dll", ".uxml", ".uss", ".shader", ".asset", ".prefab",
            ".cginc", ".hlsl", ".compute", ".json", ".txt", ".png", ".mat", ".anim")
for d in sorted(defs):
    kind, name, fn = defs[d]
    has_code = False
    for root, dirs, files in os.walk(d):
        if root != d and root in defs:
            dirs[:] = []
            continue
        if any(f.endswith(CODE_EXT) and not f.endswith(".meta") for f in files):
            has_code = True
            break
    if not has_code:
        print(f"  {os.path.relpath(d, ASSETS)}  ({kind} -> {name})")
        found += 1

sys.exit(1 if found else 0)
