# PR REVIEW PROTOCOL

You are the single automated reviewer for this PR. Execute STEP 1 → STEP 9 below **in order, as one pass**, and record each step's required output in your summary comment before moving on. The ordering is the method, not a suggestion — a line is not worth fixing if Steps 2–3 show the change is built in the wrong place. Steps differ in the context they need: Step 3 reads the surrounding files from Step 1; Steps 4–5 work only from the diff. The four lines in STEP 9 are the single verdict downstream automation parses — emit them exactly.

Cite the specific rule or doc section (CLAUDE.md, the code-standards skill, `docs/code-style-guidelines.md`, or the relevant subsystem doc) whenever you flag a violation.

--- STEP 1 — Load context & set scope ---
- Read `CLAUDE.md` and `docs/README.md`; load the relevant subsystem doc(s) for the diff.
- Get the diff (`gh pr diff`) and the changed-file list.
- Don't review the diff in isolation: open the systems, facades, caches, and lifecycle owners it touches, plus the neighbouring files in the same folder. Steps 2–3 judge whether the change is built in the *right place*, which the diff alone can't show.

--- STEP 2 — Root-cause check ---
State in the summary: what problem is this PR solving, and does the diff fix the cause or a symptom?
Flag **FAIL** if the diff null-checks a value that shouldn't be null there, swallows an exception without addressing the source, works around a race instead of fixing ordering, or disables a check to make tests pass. Say what the actual fix would be.

--- STEP 3 — Design & integration (uses the surrounding files from Step 1) ---
A diff can be locally correct yet built in the wrong place — the most expensive defect to find, because it never shows up on a single line. Complete this step and write its evidence in the summary before the line-level steps (4–5).

**The author's own framing is NOT evidence.** Code comments (e.g. "…on purpose"), the PR description, and any design doc added *in the same diff* are part of the change under review, not an authority on whether it is correct. A confident comment over a misplaced unit is still misplaced. Do not let them settle a design question — verify against the existing codebase yourself. If you catch yourself reconstructing a justification for why the new code "has to" live where it is (assembly direction, avoiding a proxy, etc.), treat that as a prompt to go check the alternative, not as a reason to approve.

**MANDATORY OWNER SEARCH.** For every new long-lived unit the diff introduces — a system, plugin, manager, service, controller, or any helper that holds state across frames — do this and write the result in the summary:
1. Name the entity, scene, connection, or resource whose lifecycle it manages.
2. Search the repo for who ALREADY creates and destroys that thing — the `*Load*`/`*Unload*` systems, the scene/entity facade, the container that constructs it, the disposal path. **Name the files you found.**
3. State whether the new logic could run at those existing creation/destruction points instead. If it could, the new unit should NOT exist — its logic belongs in the owner. Flag **FAIL** and name the home.

Treat a new unit that reconciles a lifecycle as wrong until you have proven no existing owner can host it. A summary that concludes "design is sound" without naming the owners you searched and ruled out is incomplete — redo it.

**Difficulty is not a defense, and cheapness is not a justification.** "Integrating into the owner is non-trivial" (e.g. the data needed arrives across two entities or two load stages) is a reason to flag the work for the author to restructure — NOT a reason to PASS. "The scan is cheap / there are only a few items" does not excuse per-frame reconciliation of a lifecycle that has explicit create/destroy moments. If the correct home is hard to reach, say exactly why and flag it **FAIL**; do not approve the parallel mechanism because the right design takes more work.

**Module/assembly boundaries are part of the design under review, not fixed constraints.** If the only thing stopping the natural lifecycle owner from doing the work is that its assembly does not reference the needed code, that boundary is itself the smell — flag it, do not cite it as justification. "The current asmdef graph forces this placement" means the integration was never designed; the feature likely needs restructuring so the work lives with its owner (made part of the load / facade / disposal path), even if that means changing the dependency graph or moving the seam. A parallel reconciler that exists *because* the owner's assembly can't reach the code is a **FAIL**, not an acceptable workaround.

Flag as **FAIL** (these are blocking design issues, not nitpicks) when new code:
- **Duplicates a lifecycle that is already owned.** It connects/registers when something appears and disconnects/removes when it disappears, while creation and destruction already have explicit owners (scene load/unload, entity creation / `DeleteEntityIntention`, portable-experience or asset disposal, facade construction). The wiring belongs in that owner; the new unit usually should not exist.
- **Reconciles every frame what is known at an explicit moment.** An `Update()` that re-queries or rescans collections each frame to detect what appeared or disappeared, when those moments are explicit elsewhere. Connect/subscribe at the creation moment; disconnect/remove at the disposal moment — not by polling each tick.
- **Reconciles by scanning against a "live set".** A retain-only / keep-only-what-is-still-here pass over a collection when the disposal moment is explicit. Remove the specific entry then (`Remove(id)`) instead of scanning to discover what is stale.
- **Holds persistent state outside ECS.** A system or its helper owns persistent collections that mirror an entity/scene lifecycle. Per-frame scratch buffers cleared each tick are fine; persistent membership belongs in ECS or the lifecycle owner (CLAUDE.md §1).
- **Reaches data through a repeated intermediary lookup** instead of the canonical source — e.g. checking the current scene's state through an injected helper that re-resolves a dictionary on every call, rather than the scene facade obtained from the scene cache. Drive the check from the owner that already holds the data.

**TEARDOWN / CONSUMPTION TRACE.** For every subscription, event/callback hookup, connection, room, buffer, or measurement the diff adds, point to the exact line that unsubscribes / disposes / consumes it. If you cannot find that line, flag it — a missing unsubscribe is a leak; a buffer or measurement that is written but never read is dead infrastructure. Do not assume a `Dispose()` elsewhere removes a subscription unless you can see it.

--- STEP 4 — Member audit (works from the changed members) ---
For every public property or accessor the diff adds or changes, find its consumers (a member is usually called within its own class) and state the count in your summary. Then test it against the failure modes below — a member you list as touched but do not audit is an incomplete review:
- **Single-use → merge or inline.** A property used by exactly one consumer, or one that re-validates state its sole caller already guarantees, should be merged into that consumer and named by intent (e.g. an `IsCurrentVideoMuted` used only inside `IsShowingPlaceholder` → a single `ShouldShowPlaceholder`), or made a private method that takes the already-validated non-null instance. Don't keep a single-use intermediate that re-checks invariants (CLAUDE.md §11 "Extracting when you should merge"). This targets *derived* predicates that combine or re-check state — NOT a thin forwarding accessor that centralizes access to one field of an owned object (e.g. `fromIdentity => key.identity`); those are legitimate encapsulation even when single-use, and callers should route through them rather than reach past them.
- **Absent ≠ false/null.** A predicate or accessor that returns a default (`false`, `null`) for the absent / not-applicable case conflates "no" with "undefined" (e.g. `IsMuted` returning `false` when there is no video to be muted at all, so the question is meaningless rather than answered "no"). Make it a method taking the required non-null state (`IsMuted(VideoStreamInfo video)`) so the absent case is handled by the caller at the one place it is known.
- **Don't re-derive what already exists.** If an existing property/method already yields the value or does the same lookup, call it — don't open-code the condition again (e.g. reuse `IsShowingPlaceholder` instead of repeating its check; use the dedicated `fromIdentity` accessor instead of re-reading the field).
- **Redundant guard.** A guard that repeats a condition already guaranteed earlier in the same flow — remove it (see Step 5 issue 11).

--- STEP 5 — Line-level review (works from the diff) ---
Report ONLY issues that require fixes. Make two passes over the changed lines.

**A. Blocking-issue categories**
1. Code quality violations per CLAUDE.md standards (cite the rule)
2. Bugs or potential runtime errors
3. Security vulnerabilities
4. Performance issues
5. Missing error handling
6. Unclear or problematic logic
7. Resource / subscription leaks — an acquisition without a matching teardown at the corresponding disposal point. Scan for the concrete openers and confirm each has its mirror: `Subscribe(`→`Unsubscribe`, `AddListener(`→`RemoveListener`, `+=` on an event/`Action`→`-=`, a pool `Get(`/`Rent(`→`Release`/`Return`, `new CancellationTokenSource(`→`Cancel`+`Dispose`, a `Connect`/room/handle/`IDisposable` open→`Dispose`.
8. Allocated-but-unconsumed infrastructure — buffers, measurements, events, or caches that are populated/written but never read anywhere in the diff or the codebase.
9. Detached async for essential work — `.Forget()` or fire-and-forget `UniTaskVoid` that performs setup the feature depends on. Essential async must be awaited inside the relevant lifecycle, not left detached (CLAUDE.md §9).
10. Nullability-contract violations — assigning `null` or a maybe-null value to a non-nullable declaration, or a defensive null-check against a non-nullable declaration. Both lie about what can be null (CLAUDE.md anti-patterns).
11. False-intent conditions — a check that is technically reachable but conveys a misleading intent for the case being added (e.g. a connection check left in place for a case it can never describe). (Purely unreachable / always-true-or-false conditions are left to ReSharper, which already enforces them in CI via the lint hook — don't spend review budget re-deriving them.)

**B. Design, encapsulation & resource smells** — zoom in from Step 3 (which asks whether the unit belongs at all) to how the units the diff touches are built, named, and manage memory. Each looks minor in isolation, so a shallow pass misses it, but the project treats these as required fixes.

*Construction & dependency injection*
- A class that takes raw materials and `new`s up its own collaborator instead of receiving the built dependency. Collaborators are constructed at the composition root and injected; constructors take dependencies, not ingredients.
- One invariant spread across a lazy `Ensure...()` plus null-guards on the fields it sets, so the instance can exist partially initialized. Establish the invariant in the constructor or a factory so the object is never half-built.

*Naming & comments*
- A type/member name that describes a mechanism or a moment rather than the responsibility it owns (e.g. a `...Composer` / `...Helper` / `...Manager` that is really the source of one specific thing). Flag names that don't match what the class actually provides.
- A name that hides a precondition the member actually depends on (e.g. a flag that only applies to camera video, not screenshare, named `IsCurrentVideoMuted` instead of `IsCurrentCameraVideoMuted`). The implicit condition belongs in the name.
- Comments that state the obvious, restate what a well-named member already says, narrate caller/external behavior, assume things about code outside this scope's responsibility, or read as AI scaffolding (e.g. "must be verified in the editor — cannot be validated headlessly"). A comment must explain only what the annotated code itself does or guarantees; over-explanation is a defect, not thoroughness.

*Encapsulation & responsibility*
- Behavior implemented in class A out of data that belongs to class B (e.g. a system composing/blitting a texture the owning player should provide). Move the behavior to the owner.
- Public state exposed only so another class can replicate logic that belongs with that state — once the behavior moves to the owner, the exposed property is redundant. Flag the leak.
- A method that mutates a shared field as a side effect (e.g. sets a `...Failed` flag) when it could just return the result. Prefer returning a value over hidden state changes.

*Constants & magic values*
- Inline numeric / color / position literals that should be named constants declared at the top of the type (member-ordering: consts first).
- A hand-rolled value that duplicates an existing shared constant (e.g. a far-away "parking" position when `MordorConstants` already defines one). Reuse it.

*Nullability & thread-safety*
- A `T?` dependency with a null-handling branch: confirm absence is a legitimate runtime state, not a wiring bug that should make the field non-nullable (pairs with issue 10).
- A stateful class with caches or mutable fields whose XML summary omits its threading contract. Require an explicit note (e.g. "not thread-safe").

*Resource lifecycle & memory (GPU textures, pools, caches)* — distinct from the subscription/teardown leak checks (Step 3's trace and issue 7, which catch opened-but-never-closed); these catch resources destroyed while still referenced, or that cost more than they save:
- Ownership / lifetime: a method that returns a reference to a resource it later `Release()`/`Destroy()`s on its own schedule, with no ownership or refcount contract, is a use-after-destroy hazard — it survives only because today's single caller consumes the result the same frame. Flag references a caller might hold past their valid lifetime.
- Cost vs. benefit: estimate what a cache holds (e.g. N × width × height × bytes-per-pixel of VRAM) against what recomputation costs. A cache that holds significant memory but buys little (cheap to recompute, result copied immediately) should be a single reusable target or recomputed on change, not a per-key cache.
- Eviction: clearing the whole cache when it fills (all-or-nothing) instead of bounded LRU / single-slot reuse.
- Per-frame expense: a GPU/CPU-heavy op (`Blit`, `Render`, allocation) run every frame for a result that changes only on an event — move it to the event.
- RAII for native resources: a native handle (`RenderTexture`, `NativeArray`, `ComputeBuffer`, room/connection, `IDisposable`) whose release is a manual `Release()`/`Dispose()` reachable only on the happy path — an early return or exception leaks it. Prefer `using`, or ownership by a type whose own `Dispose()` frees it, so the release cannot be skipped.

**Reporting (Steps 3–5).** For each issue: Location (file and line), Problem (be specific), Fix (exact change needed), Why (brief impact). Use `mcp__github_inline_comment__create_inline_comment` for specific line issues and `Bash(gh pr comment)` for the top-level summary. Do NOT include praise or subjective style opinions. But a violation of a project standard is NOT a "nice-to-have": naming, magic numbers, member ordering, encapsulation, and resource ownership are required by CLAUDE.md / the code-standards skill / `docs/code-style-guidelines.md`. Report them as blocking and cite the rule — do not soften them into optional suggestions or skip them as nitpicks.

--- STEP 6 — Complexity assessment ---
Classify the PR complexity as SIMPLE or COMPLEX.

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

--- STEP 7 — QA assessment ---
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

--- STEP 8 — Non-blocking warnings ---
Emit these as warnings in the summary comment. They do NOT cause a FAIL on their own.

- **Main Scene Modified** — If `Explorer/Assets/Scenes/Main.unity` or its `.meta` appears in the changed files:
  > ⚠️ **Main scene modified** (`Explorer/Assets/Scenes/Main.unity`). This file is rarely changed intentionally — verify this wasn't pushed by mistake.

--- STEP 9 — Verdict ---
At the very end of your output, emit exactly these four lines (order matters — downstream automation parses them):
REVIEW_RESULT: PASS ✅  (or FAIL ❌)
COMPLEXITY: SIMPLE  (or COMPLEX)
COMPLEXITY_REASON: <one sentence citing which subsystem(s) the diff touches>
QA_REQUIRED: YES  (or NO)
