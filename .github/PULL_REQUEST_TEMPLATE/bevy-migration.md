<!--
Bevy migration PR. Open with this template through
  https://github.com/decentraland/unity-explorer/compare/dev...<branch>?template=bevy-migration.md
Rules: BevyMigration/docs/README.md § 0 and § 5. One capability from one step per PR,
about 800 changed lines excluding generated files, title `<type>(m<step>): <subject>`,
for example `feat(m3): URP Lit transparency modes`.
-->

## Step and gate

- Step: `m<N>` (<step name from BevyMigration/docs/README.md § 7>)
- Gate: `G-<phase><n>`

## What does this PR change?
<!-- The one capability this PR implements, and the design section it follows. -->

## What the gate proves
<!--
The automated check that passes because of this change: a test, a recording replay,
a screenshot case, or the existing gate that already covers it. If this paragraph cannot
be written, the PR is too big. A PR that cannot name its gate is closed, not reviewed.
-->

## Test instructions
<!-- How a reviewer reproduces the gate locally, copy-pasteable. -->

```bash
# Rust side
cd BevyMigration/Engine && cargo fmt --check && cargo clippy --all-targets --locked -- -D warnings && cargo test --locked
# .NET side
cd BevyMigration/Explorer.Net && dotnet test Explorer.Net.sln
# Ledger
python3 BevyMigration/ledger/ledger.py validate
```

## Quality checklist
- [ ] Only one step and one gate; no Unity-side and Rust-side changes mixed unless the ABI changed
- [ ] Old path intact behind a flag when this replaces an existing capability (plan § 5, "Flags, not reverts")
- [ ] `allowlist.toml` unchanged, or this PR also adds the shim implementation and its recording
- [ ] No `// ReSharper disable` comments, no `scripts/lint/filter-warnings.sh` changes

<!--
Ledger footer, required on every PR that touches BevyMigration/** or
Explorer/Assets/DCL/BevyMigration/**. Keep it as the last line(s). One of:
  Ledger: none - <why no row changes>
  Ledger: <id> -> <status> (<gate>)
Several moves: one `Ledger:` line each, or `;`-separated on one line.
-->
Ledger: none - <why no row changes>
