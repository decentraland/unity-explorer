#!/usr/bin/env python3
"""Merge per-drv verdict.lock.json files into the main PROVENANCE.lock."""
import json, glob, os

os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
main_path = "PROVENANCE.lock"
entries = {e["name"]: e for e in json.load(open(main_path))}

for lock in sorted(glob.glob("drv/*/verdict.lock.json")):
    for e in json.load(open(lock)):
        entries[e["name"]] = e
        print(f"merged {e['name']}: {e['verdict']} (from {lock})")

merged = sorted(entries.values(), key=lambda e: e["name"])
with open(main_path, "w") as f:
    json.dump(merged, f, indent=2)
print(f"\nPROVENANCE.lock now has {len(merged)} entries:")
for e in merged:
    d = e.get("detail", {})
    dyn = d.get("dynsym_match", "")
    print(f"  {e['name']}: {e['verdict']}" + (f" (dynsym_match={dyn})" if dyn != "" else ""))
