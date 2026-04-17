Read `CLAUDE.md` and `docs/README.md` before reviewing. Load the relevant subsystem doc for the diff. Cite the specific rule or doc section when flagging a violation.

--- ROOT-CAUSE CHECK ---
Before listing issues, state in the summary comment: what problem is this PR solving, and does the diff fix the cause or a symptom?
Flag as FAIL if the diff null-checks a value that shouldn't be null there, swallows an exception without addressing the source, works around a race instead of fixing ordering, or disables a check to make tests pass. Say what the actual fix would be.

--- BLOCKING ISSUES ---
Report ONLY issues that require fixes:
1. Code quality violations per CLAUDE.md standards (cite the rule)
2. Bugs or potential runtime errors
3. Security vulnerabilities
4. Performance issues
5. Missing error handling
6. Unclear or problematic logic

For each issue found:
- Location: File and line number
- Problem: What is wrong (be specific)
- Fix: Exact change needed
- Why: Brief explanation of the impact

Use mcp__github_inline_comment__create_inline_comment for specific line issues.
Use Bash(gh pr comment) for a top-level summary comment.
Do NOT include praise, style preferences, or nice-to-have suggestions.

--- COMPLEXITY ASSESSMENT ---
After reviewing the diff, classify the PR complexity as SIMPLE or COMPLEX.

A PR is SIMPLE when ALL of the following are true:
- Touches 3 or fewer files with under ~150 lines of meaningful changes
- Changes are straightforward: typo fixes, config tweaks, log/comment changes,
  dependency bumps, small bug fixes with obvious cause and fix
- Does not modify ECS systems, components, queries, or system execution order
- Does not touch entity structural changes (Add/Remove component operations)
- Does not alter cross-world ECS access, propagation systems, or bridge components
- Does not modify async/UniTask patterns, cancellation flows, or Result types
- Does not change plugin registration, containers, dependency injection, or assembly definitions
- Does not touch the avatar rendering pipeline (GPU skinning, GVB, wearable loading, compute shaders)
- Does not modify scene runtime, CRDT synchronization, or JS module system
- Does not affect asset promise lifecycle, memory budgeting, pooling, or cache unloading
- Does not change networking, LiveKit rooms, movement encoding, or multiplayer sync
- Does not alter physics, player/camera singletons, or input handling
- Does not modify auth, web3 signing, or security-sensitive code paths

A PR is COMPLEX when ANY of the following are true:
- Touches core ECS infrastructure (systems, components, queries, world management)
- Modifies cross-world access patterns (global ↔ scene propagation, PersistentEntities)
- Changes component structural operations (ref invalidation risk from Add/Remove)
- Alters async flow (new UniTask/UniTaskVoid, cancellation, SuppressToResultAsync)
- Modifies plugin/container wiring, system registration, or assembly structure
- Touches avatar system, scene runtime, CRDT bridge, or asset loading pipeline
- Changes networking, multiplayer sync, or movement interpolation
- Affects memory management, object pooling, or resource cleanup paths
- Modifies shared interfaces, public APIs, or abstractions used across assemblies
- Introduces new dependencies or changes dependency injection
- Risk of silent data corruption or subtle regressions is non-trivial
- Large diff (4+ files or significant logic changes)

--- QA ASSESSMENT ---
Determine whether QA (manual testing) is needed for this PR.

QA_REQUIRED: NO when ALL of the following are true:
- Changes are limited to CI/CD workflows (.github/), scripts, documentation,
  config files, or other non-runtime code
- No user-facing behavior is affected (UI, rendering, gameplay, input, audio, etc.)
- No changes to code that runs in the Unity player at runtime

QA_REQUIRED: YES when ANY of the following are true:
- Changes could affect what the user sees, hears, or interacts with
- Modifies runtime code (anything under Explorer/ that ships in the build)
- Touches UI, rendering, avatars, scenes, networking, or player-facing systems
- Changes asset loading, authentication, or navigation flows

--- NON-BLOCKING WARNINGS ---
Emit these as warnings in the summary comment. They do NOT cause a FAIL on their own.

- **Main Scene Modified** — If `Explorer/Assets/Scenes/Main.unity` or its `.meta` appears in the changed files:
  > ⚠️ **Main scene modified** (`Explorer/Assets/Scenes/Main.unity`). This file is rarely changed intentionally — verify this wasn't pushed by mistake.

IMPORTANT: At the very end of your output, emit exactly these four lines (order matters — downstream automation parses them):
REVIEW_RESULT: PASS ✅  (or FAIL ❌)
COMPLEXITY: SIMPLE  (or COMPLEX)
COMPLEXITY_REASON: <one sentence citing which subsystem(s) the diff touches>
QA_REQUIRED: YES  (or NO)
