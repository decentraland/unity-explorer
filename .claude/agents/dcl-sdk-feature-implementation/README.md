# DCL SDK Feature Implementation Agents

A set of 5 coordinated Claude agents for implementing new Decentraland SDK components across all 4 repositories in one orchestrated session.

## Agent Overview

| Agent | Role | Repo |
|-------|------|------|
| **`dcl-sdk-feature-architect`** | Orchestrator — plans, delegates, verifies | (all repos) |
| `dcl-protocol-specialist` | Creates/modifies `.proto` schema files | `../protocol` |
| `dcl-sdk-specialist` | TypeScript SDK code, helpers, exports, tests | `../js-sdk-toolchain` |
| `dcl-explorer-specialist` | Unity C# ECS systems, plugin, cleanup, tests | `.` (unity-explorer) |
| `dcl-test-scene-specialist` | SDK7 test scene to validate the component | `../sdk7-test-scenes` |

**Always use `dcl-sdk-feature-architect` as your entrypoint.** It handles orchestration — you never need to invoke the specialists directly.

---

## Prerequisites

All 4 repos must be cloned as siblings. Each repo should already be on the correct starting branch:

```
parent-dir/
├── protocol/           ← must be on `experimental` or a branch derived from it
├── js-sdk-toolchain/   ← must be on `experimental` or a branch derived from it
├── unity-explorer/     ← current working directory (this repo)
└── sdk7-test-scenes/   ← any branch is fine; will use local SDK path links
```

> **Why `experimental`?** Unity-explorer always requires a protocol that is `experimental` or branches from it — using `main` alone will cause missing component files that break compilation. The SDK toolchain follows the same convention for experimental components.

---

## Recommended Workflow

### Step 1 — Write a feature description file

Create a `feature-description.md` at the root of `unity-explorer`. The more precise this file is, the less back-and-forth you'll need during plan review.

**Suggested structure:**

```markdown
# Feature: <Feature Name>

## Overview
One paragraph describing what this feature does and why it's needed in the SDK.

## Components

### PBYourComponent (LWW — scene writes, Explorer reads)
This component controls X behavior on an entity.

Fields:
- `optional float speed = 1;`  // default 1.0 — movement speed multiplier
- `optional bool enabled = 2;` // default true
- `oneof shape { PointShape point = 10; SphereShape sphere = 11; }`

Component ID: 1401 (experimental range — verify with `make check-component-id`)

### PBYourComponentResult (GOVS — Explorer writes, scene reads)
Result component that reports events back to the scene.

Fields:
- `uint32 timestamp = 1;`
- `YourEventType event_type = 2;`

Component ID: 1402 (experimental range)

## Behavior
- Describe how the Explorer should interpret and apply each component at runtime
- Note any asset lifecycle, pooling, or cleanup requirements
- Describe any scene-current guard needs or cross-system ordering constraints
- Describe how the result component is populated (what triggers writes, CRDT APPEND vs PUT)

## Reference Components
List similar existing components to guide implementation style, e.g.:
- PBLightSource (oneof variants with pooled objects + asset promises)
- PBVideoPlayer + PBVideoEvent (LWW input + GOVS result pair)
```

A feature can involve any number of components — add a section per component. The architect uses this to plan the full implementation.

### Step 2 — Invoke the architect

From your `unity-explorer` directory, start a **session-wide agent** using the `--agent` flag:

```bash
claude --agent dcl-sdk-feature-architect --permission-mode plan
```

This makes the architect's orchestration protocol the session's system prompt — pre-flight confirmation, plan review, and specialist delegation all run as designed. Using `@dcl-sdk-feature-architect` as an inline mention does **not** work for orchestrators: the parent session retains control and will ignore the agent's multi-phase workflow.

Once inside the agent session, describe your feature:

```
Read @feature-description.md and create a plan to implement that DCL SDK feature.
After we iterate the plan you will have to implement it using the specialist
sub-agents you have for the different repos.
```

The architect will:
1. Ask you to confirm repo paths and the branch name to use across all repos
2. Present a numbered execution plan showing which agents run in parallel vs sequentially
3. Wait for your approval before executing anything

### Step 3 — Review and iterate the plan

Read the plan carefully before approving. This is the cheapest moment to catch mistakes — corrections at this stage cost nothing, corrections after agents have written code across 4 repos are expensive.

Things to check:
- Are component IDs in the right range?
  - `12xx` — main branch components
  - `14xx` — experimental branch components (most common for new work)
  - `16xx` — Protocol Squad experimental components
- Do field types reuse existing common types (`Vector3`, `Color4`, `FloatRange`, etc.) instead of redefining them?
- Is the LWW vs GOVS classification correct for each component?
- Does the plan cover the full component lifecycle: instantiation, update, component removal, entity destruction, world disposal?
- Are there any `oneof` variants that need an extended TypeScript helper in the SDK?
- Is the execution order correct — does the test scene wait for the SDK build?

Iterate with the architect in plain conversation until the plan looks right, then confirm execution.

### Step 4 — Execution (fully automated)

The architect spawns specialist sub-agents in the correct order:

```
[Sequential]  dcl-protocol-specialist    — proto file(s), branch verified from experimental,
              |                            make test must pass before handing off
              ↓
[Parallel]    dcl-sdk-specialist          — TypeScript SDK (make build + make test)
              dcl-explorer-specialist     — Unity C# ECS (npm run build-protocol)
              |
              ↓
[Sequential]  dcl-test-scene-specialist   — SDK7 test scene (npm run build)
              ↓
[Architect]   Cross-layer verification checklist
```

Each specialist has a **completion gate** — it must build successfully with zero errors before reporting done. The architect will surface any failures rather than silently continuing.

All cross-repo dependencies use **local path linking by default** — no PRs or CI cycles needed to test the full stack locally.

### Step 5 — Test interactively in Unity

With the agents done and everything built, open Unity and test the component end-to-end:

1. Open Unity Editor with the `unity-explorer` project
2. Run the sdk7-test-scenes test scene in Play mode (via `npm run start --explorer-alpha`)
3. Connect the Explorer to the localhost and verify component behavior matches the feature description

This is the only step that requires human eyes — the agents cannot drive the Unity Editor.

### Step 6 — Commit and PR (when you're ready)

The agents will not commit or push anything. When you're satisfied with the implementation:

1. Review `git diff` in each repo to confirm the changes look right
2. Create branches and PRs in the correct merge order:
   - Protocol PR first (CI bot message will expose the PR package to be installed in js-sdk-toolchain and Explorer)
   - SDK + Explorer PRs (SDK PR CI bot message will expose the PR package to be installed in the test scene) 
   - Test scene PR last

See `docs/how-to-implement-new-sdk-components.md` for the full merge order guide.

---

## Tips

**Iterate the plan, not the code.** The architect presents the plan before writing a single line. Use that moment — asking for a revised plan is free, asking to redo code across 4 repos is not.

**One feature description file per feature.** Keep it at the repo root and update it as decisions are made. It becomes a useful reference when writing commit messages and PR descriptions later, and it gives the architect a single source of truth to re-read if the session is interrupted.

**Name your feature branch consistently.** The architect will use the same branch name across all 4 repos (e.g., `feat/your-component`). Pick a clear name upfront at pre-flight.

**Multiple components in one session is fine.** The feature description supports any number of component sections. The architect will plan them as a single unit and ensure component IDs are all unique and correctly ranged.

**Re-running a specialist is safe.** If a specialist's output needs revision, tell the architect what to fix and it will re-delegate. Generated files are always rebuilt from source — there is no stale-state risk.

**The cross-layer verification checklist matters.** The architect runs it at the end: component ID consistency, proto ↔ C# ↔ TypeScript field alignment, LWW/GOVS classification, and default value agreement. Read its output carefully — it will catch mismatches before you hit Unity compile errors or CRDT bugs.

**If you only need one or two repos touched**, say so in your prompt. The architect will skip irrelevant phases and spawn only the needed specialists.

**If a build fails mid-session**, the architect will report the error rather than continuing. You can ask it to investigate and retry, or fix the issue manually and tell the architect to pick up from where it left off.
