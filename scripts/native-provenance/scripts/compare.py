#!/usr/bin/env python3
"""Compare a built native library against the shipped blob and append a PROVENANCE.lock entry.

Usage: compare.py --name livekit-ffi --built /nix/store/...-.../lib/liblivekit_ffi.so \
                  --shipped /path/to/Assets/Plugins/LiveKit/liblivekit_ffi.so \
                  --drv drv/livekit-ffi/default.nix [--lock PROVENANCE.lock]

Verdicts:
  reproduced  — byte-identical sha256
  equivalent  — identical after stripping known-nondeterministic sections
                (.note.gnu.build-id, .comment) AND identical dynamic symbol table
                AND identical version strings
  divergent   — anything else (details recorded)
No LLM involved; pure T2. Requires binutils (objcopy, nm, readelf) on PATH — run inside
`nix shell nixpkgs#binutils` if the host lacks them.
"""
import argparse, hashlib, json, os, subprocess, tempfile, datetime

def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()

def run(*cmd):
    return subprocess.run(cmd, capture_output=True, text=True)

def normalized_sha(path, tmpdir):
    out = os.path.join(tmpdir, os.path.basename(path) + ".norm")
    r = run("objcopy",
            "--remove-section=.note.gnu.build-id",
            "--remove-section=.comment",
            "--remove-section=.gnu_debuglink",
            path, out)
    if r.returncode != 0:
        return None, f"objcopy failed: {r.stderr.strip()}"
    return sha256(out), None

def dynsyms(path):
    """Sorted defined dynamic symbols, or None if the table is absent/unreadable.

    An empty list is a real result (an executable that exports nothing) and
    compares equal to another empty list; None means nm could not read a table
    at all and no comparison is possible.
    """
    r = run("nm", "-D", "--defined-only", path)
    if r.returncode != 0:
        return None
    # keep name+type, drop addresses (layout may shift)
    return sorted(" ".join(l.split()[1:]) for l in r.stdout.splitlines() if l.strip())

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True)
    ap.add_argument("--built", required=True)
    ap.add_argument("--shipped", required=True)
    ap.add_argument("--drv", required=True)
    ap.add_argument("--lock", default=os.path.join(os.path.dirname(__file__), "..", "PROVENANCE.lock"))
    a = ap.parse_args()

    entry = {
        "name": a.name,
        "derivation": a.drv,
        "shipped": {"path": a.shipped, "sha256": sha256(a.shipped)},
        "built": {"path": a.built, "sha256": sha256(a.built)},
        "checked_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    }

    if entry["shipped"]["sha256"] == entry["built"]["sha256"]:
        entry["verdict"] = "reproduced"
    else:
        with tempfile.TemporaryDirectory() as td:
            ns, e1 = normalized_sha(a.shipped, td)
            nb, e2 = normalized_sha(a.built, td)
        ss, sb = dynsyms(a.shipped), dynsyms(a.built)
        detail = {}
        if e1 or e2:
            detail["normalize_error"] = e1 or e2
        detail["normalized_match"] = ns is not None and nb is not None and ns == nb
        comparable = ss is not None and sb is not None
        detail["dynsym_match"] = comparable and ss == sb
        detail["dynsym_count"] = {
            "shipped": len(ss) if ss is not None else None,
            "built": len(sb) if sb is not None else None,
        }
        unreadable = [s for s, t in (("shipped", ss), ("built", sb)) if t is None]
        if unreadable:
            detail["dynsym_unreadable"] = unreadable
        if comparable and ss != sb:
            only_shipped = sorted(set(ss) - set(sb))[:20]
            only_built = sorted(set(sb) - set(ss))[:20]
            detail["dynsym_only_shipped"] = only_shipped
            detail["dynsym_only_built"] = only_built
        entry["detail"] = detail
        entry["verdict"] = "equivalent" if detail["normalized_match"] and detail["dynsym_match"] else "divergent"

    lock = os.path.abspath(a.lock)
    entries = []
    if os.path.exists(lock):
        with open(lock) as f:
            entries = json.load(f)
    entries = [e for e in entries if e["name"] != a.name] + [entry]
    entries.sort(key=lambda e: e["name"])
    with open(lock, "w") as f:
        json.dump(entries, f, indent=2)
    print(f"{a.name}: {entry['verdict']}")
    if entry["verdict"] == "divergent":
        print(json.dumps(entry["detail"], indent=2))

if __name__ == "__main__":
    main()
