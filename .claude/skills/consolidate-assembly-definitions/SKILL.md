---
name: consolidate-assembly-definitions
description: Use when merging, folding, renaming, or removing asmdef/asmref assemblies in this project — reducing assembly count, converting an .asmdef to an .asmref, moving code between assemblies, or reviewing assembly structure for redundant references and cycles.
---

# Consolidate Assembly Definitions

## Overview

Folding an assembly X into an anchor Y = delete `X.asmdef`, point an `.asmref` at Y (or nothing, if X's folder is inside Y's tree), retarget every reference. **Every merge must be cycle-simulated BEFORE touching files** — Unity forbids reference cycles and the transitive graph hides them. Verification gate = clean batch compile (never the full test suite).

Scripts live in `scripts/` next to this skill. Run `regen-graph.ps1` first — never trust stale assumptions about current references.

## Workflow

1. **Regen graph**: `scripts/regen-graph.ps1` → `graph.json` (refs resolved to names), `asmrefs.json`, `guidmap.txt`. Record the UNRESOLVED count (external packages — acceptance is "no NEW unresolved", not zero).
2. **Simulate**: `scripts/simulate-merge.ps1 -Anchor Y -Members X1,X2`. CYCLE → don't fold; check if folding the cycle-edge member *simultaneously* dissolves it (e.g. Backpack alone cycled via `RewardPanel → Backpack`; Backpack+RewardPanel together was clean).
3. **Fold**: `scripts/fold.py <plan.json>` (declarative `{folds:[{member,anchor}], renames:[{asmdef,newName}]}`). It deletes asmdef+meta, writes asmref+fresh-guid meta, retargets all referencing asmdefs/asmrefs, unions refs/unsafe/precompiled into the anchor.
4. **Reconcile by hand** (fold.py doesn't do these):
   - `csc.rsp`: delete member copies identical to the anchor's; if the anchor lacks one and a member had `-nullable:enable`, move it to the anchor folder. csc.rsp next to an `.asmref` is dead — Unity ignores it.
   - `InternalsVisibleTo`: grep for grants TO folded/renamed names → retarget. Grants inside folded folders move with the code (duplicates are legal).
   - Renames only: grep `link.xml` fullname entries, asmdef name-form refs, and serialized assets for `Type, OldAssemblyName` strings.
5. **Hygiene scan**: `scripts/scan-hygiene.py` — must report zero redundant asmrefs (ancestor already provides the same assembly) and zero code-less assembly folders. Delete empty folders entirely (+ folder .meta).
6. **Verify**: regen graph (expected count, no new unresolved, DFS cycle-free) + **batch compile only**: `Unity.exe -batchmode -quit -projectPath Explorer -logFile <log>`; zero `error CS` + "Exiting batchmode successfully". Editor must be closed. Do NOT run the EditMode suite.
7. One commit per fold group, concise bullets, skips documented with the blocking chain.
8. **Docs once at plan end** (not per iteration): refresh the layout tables + counts in `docs/directories-and-assemblies-structure.md`.

## Quick reference

| Situation | Action |
|---|---|
| Member folder's nearest ancestor assembly == the anchor | Delete member asmdef, **no asmref at all** |
| Nearest ancestor is a DIFFERENT assembly (or none) | asmref `{"reference": "GUID:<anchor-guid>"}` (GUID form = repo convention) — required even when physically nested under another assembly's tree |
| `Tests/`, `Systems/`, bus subfolders with own asmref | Excluded subtrees — keep them |
| Member has `defineConstraints` anchor lacks | `#if`-guard the sources first, or don't fold |
| Member unsafe, anchor not | Set anchor `allowUnsafeCode: true` |
| Renaming an anchor | Edit `name` field + `git mv` the file; GUID (meta) must not change |
| Reference "looks dead" by grep | Re-verify: namespaces span assemblies (`DCL.Ipfs` lives in DCL.Network) — compile is the proof |

## Hard rules (each cost a broken build or review round when violated)

- **Pick anchors by domain, not just by graph**: a clean cycle simulation is necessary, not sufficient. Read the member's source first — what is this code FOR? — then choose the anchor whose purpose matches (e.g. bootstrap/debug configuration belongs with `DCL.Platform`/app config, not with whatever assembly happens to share its references).
- **UPM package references are never a fold blocker**: packages (LiveKit, UniTask, …) are always leaves — they cannot reference project assemblies, so they cannot cycle and carrying one into an anchor is fine. Do not mirror package types or decouple package refs to "protect" an anchor; only PROJECT-assembly direction matters.
- **Preserve reusable leaves**: a natural leaf assembly (small, self-contained, several current or plausible future consumers across features) stays a leaf even when a fold simulates clean. Folding it into a fat anchor forces future consumers to take the anchor's whole tail, and splitting back out later is far costlier than keeping it now. Fold only terminal members — code consumed solely by the composition-root tier (DCL.Plugins/tests) or by exactly the anchor's own domain.
- **Lean leaves never merge upward**: Utility, DCL.Network, Web3, ECS, Realm, SceneRunner.Scene, CRDT, Character, DCL.CharacterMotion.Components, DCL.Input, DCL.Prefs (Utility refs it).
- **Production code must never reference DCL.Plugins** — only Playgrounds and tests may. If a fold target is DCL.Plugins and a production assembly consumes the member, pick another anchor.
- **Namespace shadowing**: after folds, namespaces like `DCL.Time`/`DCL.Chat` become visible to more code and shadow `UnityEngine.Time`/protobuf `Chat` inside `namespace DCL.*` blocks (CS0234/CS0118). Fix with full qualification (`UnityEngine.Time.` — repo idiom) or a using-alias **inside the namespace block** (file-level aliases lose to enclosing-namespace lookup).
- Known cycle-locked merges — do not re-attempt without decoupling first: Analytics+Implementation (`Flows → Profiles → Analytics`), Chat.History→Social (`RealmNavigation`), SceneLoadingScreens→Flows, SmartWearables→anything (`UI → SmartWearables`), Settings/MapRenderer→Social (`Backpack → Settings → Landscape`), Prefs→Platform, Hud↔Social one-way only.

## Red flags

- Creating an asmref without checking the nearest ancestor assembly first
- Folding before simulating ("the graph looked fine")
- Removing an asmdef reference because grep found no `using` (qualified usage, asmref-folded folders, namespace≠assembly)
- Running the 23k-test EditMode suite as a gate (compile is the gate; suite crashes batch mode)
- Leaving a folder whose only content is an asmref/asmdef
