#!/usr/bin/env python3
"""Ledger tooling for the Bevy migration (BevyMigration/docs/README.md § 6).

    ledger.py seed       add a row for every asmdef and feature directory that has none
    ledger.py validate   schema, enum and drift checks against Explorer/Assets
    ledger.py pr         validate, then enforce the PR footer rule (plan § 5)
    ledger.py progress   per-status counts as a Markdown table

A feature row is a direct child directory of Explorer/Assets/DCL, except that the
container directories in EXPANDED_DIRECTORIES contribute their children instead of
themselves. An asmdef row is every *.asmdef under Explorer/Assets, keyed by its name.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
DEFAULT_LEDGER = HERE / "ledger.csv"
DEFAULT_ASSETS = REPO / "Explorer" / "Assets"

COLUMNS = ["id", "kind", "target", "status", "step", "gate", "notes", "prs"]
KINDS = {"asmdef", "feature", "shim", "family"}
SOURCED_KINDS = {"asmdef", "feature"}
TARGETS = {"bevy", "shim", "web", "deleted", "keep"}
STATUSES = ["unity", "planned", "in-progress", "bevy", "shim", "web", "deleted"]
TERMINAL_STATUSES = {"bevy", "shim", "web", "deleted"}
STEPS = {str(i) for i in range(12)}
GATE_RE = re.compile(r"^G-(?:\d\d|[A-F]\d)$")

FEATURE_ROOT = "DCL"
EXPANDED_DIRECTORIES = ("Infrastructure",)

MIGRATION_PATHS = ("BevyMigration/", "Explorer/Assets/DCL/BevyMigration/")
LEDGER_PATH = "BevyMigration/ledger/ledger.csv"
FOOTER_RE = re.compile(r"^Ledger:\s*(?P<body>.*?)\s*$")
FOOTER_NONE_RE = re.compile(r"^none(?:\s.*)?$")
FOOTER_ENTRY_RE = re.compile(r"^(?P<id>\S+)\s*->\s*(?P<status>[a-z-]+)(?:\s*\((?P<gate>[^)]*)\))?$")


class LedgerError(Exception):
    pass


def discover_units(assets: Path) -> dict[str, str]:
    units: dict[str, str] = {}
    for asmdef in sorted(assets.rglob("*.asmdef")):
        name = json.loads(asmdef.read_text(encoding="utf-8-sig"))["name"]
        if name in units:
            raise LedgerError(f"two asmdefs are named {name}")
        units[name] = "asmdef"
    dcl = assets / FEATURE_ROOT
    for directory in sorted(dcl.iterdir()):
        if not directory.is_dir() or directory.name.startswith("."):
            continue
        if directory.name in EXPANDED_DIRECTORIES:
            for child in sorted(directory.iterdir()):
                if child.is_dir() and not child.name.startswith("."):
                    units[f"{FEATURE_ROOT}/{directory.name}/{child.name}"] = "feature"
        else:
            units[f"{FEATURE_ROOT}/{directory.name}"] = "feature"
    return units


def read_ledger(path: Path) -> list[dict[str, str]]:
    if not path.exists() or path.stat().st_size == 0:
        return []
    with path.open(newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != COLUMNS:
            raise LedgerError(f"{path}: header must be {','.join(COLUMNS)}, found {reader.fieldnames}")
        rows = []
        for row in reader:
            if None in row or any(value is None for value in row.values()):
                raise LedgerError(f"{path}: row {row.get('id')!r} has the wrong number of columns")
            rows.append(row)
        return rows


def write_ledger(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def validate_rows(rows: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    seen: set[str] = set()
    for row in rows:
        row_id = row["id"]
        where = f"row {row_id!r}"
        if not row_id or any(c.isspace() for c in row_id):
            errors.append(f"{where}: id must be non-empty and contain no whitespace")
        if row_id in seen:
            errors.append(f"{where}: duplicate id")
        seen.add(row_id)
        if row["kind"] not in KINDS:
            errors.append(f"{where}: kind {row['kind']!r} is not one of {sorted(KINDS)}")
        status = row["status"]
        if status not in STATUSES:
            errors.append(f"{where}: status {status!r} is not one of {STATUSES}")
        if row["target"] and row["target"] not in TARGETS:
            errors.append(f"{where}: target {row['target']!r} is not one of {sorted(TARGETS)}")
        if not row["target"] and status != "unity":
            errors.append(f"{where}: target is required once status leaves 'unity'")
        if row["step"] and row["step"] not in STEPS:
            errors.append(f"{where}: step {row['step']!r} is not 0..11")
        if not row["step"] and status != "unity":
            errors.append(f"{where}: step is required once status leaves 'unity'")
        if row["gate"] and not GATE_RE.match(row["gate"]):
            errors.append(f"{where}: gate {row['gate']!r} does not match G-<phase><n>")
        if status in TERMINAL_STATUSES and not row["gate"]:
            errors.append(f"{where}: status {status!r} requires the gate that proves it (plan § 0.5)")
    return errors


def validate_drift(rows: list[dict[str, str]], units: dict[str, str]) -> list[str]:
    errors: list[str] = []
    by_id = {row["id"]: row for row in rows}
    for unit_id, kind in units.items():
        row = by_id.get(unit_id)
        if row is None:
            errors.append(f"{kind} {unit_id!r} has no ledger row; run `python3 {LEDGER_PATH.replace('ledger.csv', 'ledger.py')} seed`")
        elif row["kind"] != kind:
            errors.append(f"row {unit_id!r}: kind is {row['kind']!r} but the source is a {kind}")
    for row in rows:
        if row["kind"] in SOURCED_KINDS and row["id"] not in units and row["status"] != "deleted":
            errors.append(f"row {row['id']!r}: no {row['kind']} with that id exists under Explorer/Assets and the row is not 'deleted'")
    return errors


def seed(ledger_path: Path, assets: Path) -> int:
    rows = read_ledger(ledger_path)
    existing = {row["id"] for row in rows}
    units = discover_units(assets)
    added = [
        {"id": unit_id, "kind": kind, "target": "", "status": "unity", "step": "", "gate": "", "notes": "", "prs": ""}
        for unit_id, kind in sorted(units.items(), key=lambda item: (item[1], item[0]))
        if unit_id not in existing
    ]
    write_ledger(ledger_path, rows + added)
    print(f"{ledger_path}: {len(rows)} rows kept, {len(added)} added ({len(units)} units discovered)")
    return 0


def validate(ledger_path: Path, assets: Path) -> int:
    rows = read_ledger(ledger_path)
    errors = validate_rows(rows) + validate_drift(rows, discover_units(assets))
    return report(errors, f"{ledger_path}: {len(rows)} rows valid")


def parse_footer(body: str) -> tuple[bool, list[dict[str, str]], list[str]]:
    none = False
    entries: list[dict[str, str]] = []
    errors: list[str] = []
    for line in body.replace("\r\n", "\n").split("\n"):
        match = FOOTER_RE.match(line.strip())
        if not match:
            continue
        footer = match.group("body")
        if FOOTER_NONE_RE.match(footer):
            none = True
            continue
        for raw in footer.split(";"):
            entry = FOOTER_ENTRY_RE.match(raw.strip())
            if not entry:
                errors.append(f"footer entry {raw.strip()!r} is not `<id> -> <status> (<gate>)` or `none`")
                continue
            entries.append({"id": entry["id"], "status": entry["status"], "gate": (entry["gate"] or "").strip()})
    if none and entries:
        errors.append("footer says `Ledger: none` and also names row moves; pick one")
    return none, entries, errors


def check_pr(rows: list[dict[str, str]], base_rows: list[dict[str, str]], changed: list[str], body: str) -> list[str]:
    errors: list[str] = []
    touches_migration = any(path.startswith(MIGRATION_PATHS) for path in changed)
    ledger_changed = LEDGER_PATH in changed
    none, entries, footer_errors = parse_footer(body)
    errors += footer_errors

    if touches_migration and not ledger_changed and not none:
        errors.append(
            "the PR changes migration paths without changing ledger.csv; "
            "add a `Ledger: none` footer line stating why no row changes (plan § 0.4)"
        )
    if none and ledger_changed:
        errors.append("the footer says `Ledger: none` but ledger.csv changed")

    base_by_id = {row["id"]: row for row in base_rows}
    moves: dict[str, dict[str, str]] = {}
    for row in rows:
        base = base_by_id.get(row["id"])
        before = base["status"] if base else "unity"
        if row["status"] != before:
            moves[row["id"]] = row

    claimed: dict[str, dict[str, str]] = {}
    for entry in entries:
        if entry["id"] in claimed:
            errors.append(f"footer names {entry['id']!r} twice")
        claimed[entry["id"]] = entry

    for row_id, row in moves.items():
        entry = claimed.get(row_id)
        if entry is None:
            errors.append(f"row {row_id!r} moves to {row['status']!r} but the footer does not name it: `Ledger: {row_id} -> {row['status']} ({row['gate'] or '<gate>'})`")
            continue
        if entry["status"] != row["status"]:
            errors.append(f"footer says {row_id!r} -> {entry['status']!r} but ledger.csv says {row['status']!r}")
        if row["status"] in TERMINAL_STATUSES and entry["gate"] != row["gate"]:
            errors.append(f"footer gate {entry['gate']!r} for {row_id!r} differs from the ledger gate {row['gate']!r}")
    for row_id in claimed:
        if row_id not in moves:
            errors.append(f"footer names a move for {row_id!r} that ledger.csv does not contain")
    return errors


def pr(ledger_path: Path, assets: Path, base_ledger: Path, changed_files: Path, body_file: Path) -> int:
    rows = read_ledger(ledger_path)
    errors = validate_rows(rows) + validate_drift(rows, discover_units(assets))
    if errors:
        return report(errors, "")
    base_rows = read_ledger(base_ledger)
    changed = [line.strip() for line in changed_files.read_text(encoding="utf-8").splitlines() if line.strip()]
    body = body_file.read_text(encoding="utf-8")
    return report(check_pr(rows, base_rows, changed, body), "ledger footer rule satisfied")


def progress(ledger_path: Path) -> int:
    rows = read_ledger(ledger_path)
    print("| status | rows |")
    print("|---|---|")
    for status in STATUSES:
        print(f"| {status} | {sum(1 for row in rows if row['status'] == status)} |")
    print(f"| total | {len(rows)} |")
    return 0


def report(errors: list[str], success: str) -> int:
    for error in errors:
        print(f"ledger: {error}", file=sys.stderr)
    if errors:
        print(f"ledger: {len(errors)} problem(s)", file=sys.stderr)
        return 1
    if success:
        print(success)
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--ledger", type=Path, default=DEFAULT_LEDGER)
    parser.add_argument("--assets", type=Path, default=DEFAULT_ASSETS, help="Explorer/Assets directory")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("seed")
    sub.add_parser("validate")
    sub.add_parser("progress")
    pr_parser = sub.add_parser("pr")
    pr_parser.add_argument("--base-ledger", type=Path, required=True, help="ledger.csv as of the PR base (may be empty)")
    pr_parser.add_argument("--changed-files", type=Path, required=True, help="one repo-relative path per line")
    pr_parser.add_argument("--body", type=Path, required=True, help="the PR description")
    args = parser.parse_args(argv)
    try:
        if args.command == "seed":
            return seed(args.ledger, args.assets)
        if args.command == "validate":
            return validate(args.ledger, args.assets)
        if args.command == "progress":
            return progress(args.ledger)
        return pr(args.ledger, args.assets, args.base_ledger, args.changed_files, args.body)
    except (LedgerError, OSError, KeyError, json.JSONDecodeError) as error:
        print(f"ledger: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
