---
name: resolve-asmdef-merge-conflicts
description: Use when merging/rebasing a branch that consolidated or renamed asmdef assemblies and the base branch changed assemblies in parallel — modify/delete conflicts on .asmdef files, dangling GUID references, new asmdefs/asmrefs added in base, code added into folded folders, or InternalsVisibleTo/link.xml entries naming old assemblies.
---

# Resolve Asmdef Merge Conflicts

## Overview

When the base branch evolves while a consolidation PR is open, conflicts are rarely just textual — the base may reference assemblies that no longer exist on the branch. **The resolution is always: keep the consolidation, port the base's *intent* onto the new structure**, then re-verify with the tooling from `consolidate-assembly-definitions` (its `scripts/` are the source of truth: `regen-graph.ps1`, `simulate-merge.ps1`, `scan-hygiene.py`).

## Procedure

1. `git merge origin/dev` (or rebase). Inventory conflicts: `git status --short | grep -E "asmdef|asmref|AssemblyInfo|link.xml|csc.rsp"`.
2. Resolve per the table below — never restore a folded asmdef to silence a conflict.
3. After ALL resolutions: `regen-graph.ps1` (count as expected, **0 unresolved** — every dangling GUID is a base-branch edit you haven't ported yet), `scan-hygiene.py` (0 redundant asmrefs / empty folders), full-graph cycle check, then the batch compile gate (`Unity.exe -batchmode -quit`, zero `error CS`; never the test suite).

## Conflict classes

| Base branch did | Resolution |
|---|---|
| Modified an `.asmdef` the branch DELETED (modify/delete conflict) | Keep the deletion (`git rm`). Diff the base's version vs the pre-fold one (`git diff <merge-base> MERGE_HEAD -- <file>`) and port the delta into the ANCHOR's asmdef: new references → union (skip now-intra/already-present); `allowUnsafeCode: true` → set on anchor; new `defineConstraints` → `#if`-guard the folded sources instead; new precompiled refs → union. |
| Added a reference to a folded/renamed assembly's GUID in some consumer | No textual conflict — caught by regen-graph as `UNRESOLVED`. Replace with the anchor's GUID (dedupe if already present). |
| Added a NEW `.asmdef` or `.asmref` | Check the nearest-ancestor rule: if the folder's governing assembly already equals the target, the new asmref is redundant — delete it. New test asmdefs → fold into `DCL.EditMode.Tests`/`DCL.PlayMode.Tests` per project rules. New feature asmdef: **fold-first** - per consolidate-assembly-definitions find the best domain anchor (read its source for purpose, cycle-sim, check consumers); keep it standalone only as a LAST RESORT (heavily-referenced new leaf or no domain-true anchor). A package (UPM) assembly reference is never an issue - packages are leaves. |
| Added source files INTO a folder that now folds into an anchor | Files compile into the anchor automatically. Risks: (a) the file `using`s an assembly the anchor doesn't reference → add the ref (cycle-sim first); (b) namespace shadowing — bare `Time.`, `Chat`, etc. now resolve to `DCL.*` namespaces (CS0234/CS0118) → qualify (`UnityEngine.Time.`) or use a using-alias INSIDE the namespace block. The compile gate surfaces both. |
| Added `InternalsVisibleTo("<old name>")` or `link.xml`/serialized `Type, OldAssembly` entries | Retarget to the new assembly name (renames in this PR: ECS→ECS.Core, AvatarShape→DCL.AvatarRendering, CharacterMotion→DCL.Character, ScreencaptureCamera→DCL.InWorldCamera, MainUi→DCL.UI.Hud, NftPrompt→DCL.UI.Prompts, AuthenticationScreenFlow→DCL.UI.Flows, LambdasService→DCL.ApiServices, NativeWindowManager→DCL.Native; folded assemblies → their anchor per `docs/directories-and-assemblies-structure.md`). |
| Edited a `csc.rsp` in a folded folder | csc.rsp next to an `.asmref` is dead. Port new flags to the anchor's csc.rsp; delete the member copy. |
| Moved/added files under old paths (`Infrastructure/ECS/SceneLifeCycle`, `ECS/StreamableLoading`, …) | Apply the base's change at the NEW path (directories moved during consolidation); `git status` shows these as add+delete pairs, not conflicts. |

## Red flags

- Restoring a deleted `.asmdef` "to make the merge compile" — port to the anchor instead
- Accepting "theirs" on an anchor asmdef wholesale — you'll silently drop the unioned refs/flags the folds added
- Declaring the merge done without a fresh `regen-graph.ps1` showing 0 unresolved — textual resolution does not catch dangling GUIDs
- Skipping the compile gate because "only JSON changed" — base-added source in folded folders can shadow-break (`DCL.Time` vs `UnityEngine.Time`)
