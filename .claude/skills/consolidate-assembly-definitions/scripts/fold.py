#!/usr/bin/env python3
"""Executes asmdef folds (member -> anchor) per the consolidation recipe.

Usage: python3 fold.py <plan-file.json> [--assets <path-to-Explorer/Assets>]

Plan file:
  {"renames": [{"asmdef": "<rel path>", "newName": "..."}],
   "folds":   [{"member": "<rel path to member .asmdef>", "anchor": "<rel path to anchor .asmdef>"}]}
Paths relative to Explorer/Assets. Renames run BEFORE folds.

Per fold: deletes member asmdef+meta, writes an asmref (GUID form) + fresh-guid meta,
retargets every asmdef/asmref referencing the member, unions the member's references,
allowUnsafeCode, and precompiledReferences into the anchor.

Does NOT handle (do by hand, see SKILL.md): csc.rsp reconciliation, InternalsVisibleTo
retargets, link.xml / serialized "Type, Assembly" strings, descendant-of-anchor
detection (it always writes an asmref; delete it afterwards if scan-hygiene flags it).
Aborts a fold if the member has defineConstraints or platform mismatch vs the anchor.
"""
import json, os, sys, uuid, io

def find_assets():
    if "--assets" in sys.argv:
        return sys.argv[sys.argv.index("--assets") + 1]
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.normpath(os.path.join(here, "..", "..", "..", "..", "Explorer", "Assets"))

ASSETS = find_assets()

def read_json(p):
    with io.open(p, "r", encoding="utf-8-sig") as f:
        return json.load(f)

def write_json(p, data):
    with io.open(p, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)
        f.write("\n")

def meta_guid(asset_path):
    with io.open(asset_path + ".meta", "r", encoding="utf-8-sig") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("no guid in " + asset_path + ".meta")

def all_files(ext):
    for root, _dirs, files in os.walk(ASSETS):
        for fn in files:
            if fn.endswith(ext):
                yield os.path.join(root, fn)

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionReferenceImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def do_rename(asmdef_rel, new_name):
    path = os.path.join(ASSETS, asmdef_rel)
    data = read_json(path)
    old_name = data["name"]
    data["name"] = new_name
    write_json(path, data)
    new_path = os.path.join(os.path.dirname(path), new_name + ".asmdef")
    if new_path != path:
        os.rename(path, new_path)
        os.rename(path + ".meta", new_path + ".meta")
    print(f"RENAMED: {old_name} -> {new_name} ({new_path})")
    return new_path

def do_fold(member_rel, anchor_path_abs):
    member_path = os.path.join(ASSETS, member_rel)
    xguid = meta_guid(member_path)
    yguid = meta_guid(anchor_path_abs)
    xdata = read_json(member_path)
    ydata = read_json(anchor_path_abs)
    xname = xdata["name"]

    if xdata.get("defineConstraints"):
        raise RuntimeError(f"ABORT {xname}: has defineConstraints {xdata['defineConstraints']}")
    if (xdata.get("includePlatforms") or []) != (ydata.get("includePlatforms") or []):
        raise RuntimeError(f"ABORT {xname}: platform mismatch vs anchor")

    os.remove(member_path)
    os.remove(member_path + ".meta")

    ref_path = os.path.splitext(member_path)[0] + ".asmref"
    with io.open(ref_path, "w", encoding="utf-8", newline="\n") as f:
        f.write('{\n    "reference": "GUID:%s"\n}\n' % yguid)
    with io.open(ref_path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META_TEMPLATE.format(guid=uuid.uuid4().hex))

    xref = "GUID:" + xguid
    yref = "GUID:" + yguid
    for p in all_files(".asmdef"):
        d = read_json(p)
        refs = d.get("references") or []
        if xref in refs:
            refs = [r for r in refs if r != xref]
            is_anchor = os.path.normcase(p) == os.path.normcase(anchor_path_abs)
            if (not is_anchor) and (yref not in refs):
                refs.append(yref)
            d["references"] = refs
            write_json(p, d)
            print(f"RETARGET asmdef: {p}")

    for p in all_files(".asmref"):
        d = read_json(p)
        if d.get("reference") in (xref, xname):
            d["reference"] = yref
            write_json(p, d)
            print(f"RETARGET asmref: {p}")

    ydata = read_json(anchor_path_abs)
    yrefs = ydata.get("references") or []
    added = []
    for r in xdata.get("references") or []:
        if r == yref or r in yrefs:
            continue
        yrefs.append(r)
        added.append(r)
    changed = bool(added)
    if xdata.get("allowUnsafeCode") and not ydata.get("allowUnsafeCode"):
        ydata["allowUnsafeCode"] = True
        changed = True
        print(f"UNSAFE: anchor {ydata['name']} set allowUnsafeCode=true (from {xname})")
    xpre = xdata.get("precompiledReferences") or []
    if xpre:
        ypre = ydata.get("precompiledReferences") or []
        for pr in xpre:
            if pr not in ypre:
                ypre.append(pr)
        ydata["precompiledReferences"] = ypre
        ydata["overrideReferences"] = True
        changed = True
        print(f"PRECOMPILED union from {xname}: {xpre}")
    if changed:
        ydata["references"] = yrefs
        write_json(anchor_path_abs, ydata)
    print(f"FOLDED: {xname} -> {ydata['name']} (union added {len(added)} refs)")

def main():
    plan = read_json(sys.argv[1])
    renamed = {}
    for r in plan.get("renames", []):
        renamed[r["asmdef"]] = do_rename(r["asmdef"], r["newName"])
    for f in plan.get("folds", []):
        anchor_abs = renamed.get(f["anchor"]) or os.path.join(ASSETS, f["anchor"])
        do_fold(f["member"], anchor_abs)
    print("ALL DONE")

if __name__ == "__main__":
    main()
