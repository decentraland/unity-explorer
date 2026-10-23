"""Unit tests for ledger.py; run with `python3 -m unittest discover -s BevyMigration/ledger`."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import ledger

LEDGER_HEADER = "id,kind,target,status,step,gate,notes,prs\n"


def make_assets(root: Path) -> Path:
    assets = root / "Explorer" / "Assets"
    (assets / "DCL" / "Chat").mkdir(parents=True)
    (assets / "DCL" / "Infrastructure" / "CRDT").mkdir(parents=True)
    (assets / "DCL" / "Chat" / "DCL.Chat.asmdef").write_text(json.dumps({"name": "DCL.Chat"}), encoding="utf-8")
    (assets / "Plugins" / "Vendor").mkdir(parents=True)
    (assets / "Plugins" / "Vendor" / "Vendor.asmdef").write_text(json.dumps({"name": "Vendor"}), encoding="utf-8")
    return assets


class LedgerShould(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.assets = make_assets(self.root)
        self.ledger = self.root / "ledger.csv"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write(self, *rows: str) -> None:
        self.ledger.write_text(LEDGER_HEADER + "".join(row + "\n" for row in rows), encoding="utf-8")

    def seeded_rows(self) -> str:
        return "\n".join([
            "DCL.Chat,asmdef,,unity,,,,",
            "Vendor,asmdef,,unity,,,,",
            "DCL/Chat,feature,,unity,,,,",
            "DCL/Infrastructure/CRDT,feature,,unity,,,,",
        ])

    def test_seed_adds_every_asmdef_and_feature_directory(self) -> None:
        self.assertEqual(0, ledger.seed(self.ledger, self.assets))

        self.assertEqual(LEDGER_HEADER + self.seeded_rows() + "\n", self.ledger.read_text(encoding="utf-8"))

    def test_seed_keeps_existing_rows(self) -> None:
        self.write("DCL/Chat,feature,web,in-progress,8,G-C1,hand edited,")

        ledger.seed(self.ledger, self.assets)

        text = self.ledger.read_text(encoding="utf-8")
        self.assertIn("DCL/Chat,feature,web,in-progress,8,G-C1,hand edited,", text)
        self.assertEqual(1, text.count("DCL/Chat,"))
        self.assertEqual(0, ledger.validate(self.ledger, self.assets))

    def test_validate_rejects_unknown_status_and_missing_gate(self) -> None:
        rows = [
            {"id": "A", "kind": "shim", "target": "shim", "status": "done", "step": "7", "gate": "", "notes": "", "prs": ""},
            {"id": "B", "kind": "shim", "target": "shim", "status": "shim", "step": "7", "gate": "", "notes": "", "prs": ""},
            {"id": "C", "kind": "shim", "target": "", "status": "planned", "step": "", "gate": "G-X1", "notes": "", "prs": ""},
        ]

        errors = ledger.validate_rows(rows)

        self.assertEqual(5, len(errors), errors)

    def test_validate_reports_drift_both_ways(self) -> None:
        self.write("DCL.Chat,asmdef,,unity,,,,", "Vendor,asmdef,,unity,,,,", "DCL/Chat,feature,,unity,,,,",
                   "Gone,asmdef,,unity,,,,", "Removed,asmdef,deleted,deleted,10,G-E1,,")

        errors = ledger.validate_drift(ledger.read_ledger(self.ledger), ledger.discover_units(self.assets))

        self.assertEqual(2, len(errors), errors)
        self.assertIn("DCL/Infrastructure/CRDT", errors[0])
        self.assertIn("Gone", errors[1])

    def test_pr_without_footer_fails_when_migration_paths_change(self) -> None:
        rows = ledger.read_ledger(self.seeded())

        errors = ledger.check_pr(rows, rows, ["BevyMigration/Engine/crates/abi/src/lib.rs"], "feat(m2): abi entry points")

        self.assertEqual(1, len(errors), errors)
        self.assertIn("Ledger: none", errors[0])

    def test_pr_with_none_footer_passes(self) -> None:
        rows = ledger.read_ledger(self.seeded())

        errors = ledger.check_pr(rows, rows, ["BevyMigration/Engine/crates/abi/src/lib.rs"], "body\r\n\r\nLedger: none - no row changes, scaffolding only\r\n")

        self.assertEqual([], errors)

    def test_pr_outside_migration_paths_needs_no_footer(self) -> None:
        rows = ledger.read_ledger(self.seeded())

        self.assertEqual([], ledger.check_pr(rows, rows, ["Explorer/Assets/DCL/Chat/ChatController.cs"], ""))

    def test_pr_status_move_requires_matching_footer(self) -> None:
        base_rows = ledger.read_ledger(self.seeded())
        self.write("DCL.Chat,asmdef,web,web,8,G-C1,,", "Vendor,asmdef,,unity,,,,", "DCL/Chat,feature,,unity,,,,", "DCL/Infrastructure/CRDT,feature,,unity,,,,")
        rows = ledger.read_ledger(self.ledger)
        changed = ["BevyMigration/ledger/ledger.csv"]

        self.assertEqual(1, len(ledger.check_pr(rows, base_rows, changed, "")))
        self.assertEqual(1, len(ledger.check_pr(rows, base_rows, changed, "Ledger: DCL.Chat -> web (G-C2)")))
        self.assertEqual(1, len(ledger.check_pr(rows, base_rows, changed, "Ledger: DCL.Chat -> bevy (G-C1)")))
        self.assertEqual([], ledger.check_pr(rows, base_rows, changed, "Ledger: DCL.Chat -> web (G-C1)"))

    def test_pr_rejects_footer_moves_the_ledger_does_not_contain(self) -> None:
        rows = ledger.read_ledger(self.seeded())

        errors = ledger.check_pr(rows, rows, ["BevyMigration/ledger/ledger.csv"], "Ledger: DCL/Chat -> web (G-C1)")

        self.assertEqual(1, len(errors), errors)

    def test_pr_with_empty_base_ledger_treats_unity_rows_as_unchanged(self) -> None:
        rows = ledger.read_ledger(self.seeded())

        self.assertEqual([], ledger.check_pr(rows, [], ["BevyMigration/ledger/ledger.csv"], ""))

    def seeded(self) -> Path:
        ledger.seed(self.ledger, self.assets)
        return self.ledger


if __name__ == "__main__":
    unittest.main()
