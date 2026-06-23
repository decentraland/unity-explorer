# CLAUDE.md

## Startup

At the start of every conversation, read [`docs/README.md`](docs/README.md) to load the project documentation map. Skills are automatically loaded by Claude Code from `.claude/skills/` — do not manually read them.

Before writing or modifying any code, follow the code-standards skill for naming conventions, member ordering, formatting rules, and test patterns. For edge cases, [`Explorer/.editorconfig`](Explorer/.editorconfig) is the authoritative formatting reference.

## Linting in the AI flow

A `Stop` hook (`.claude/settings.json` → [`scripts/lint/lint-changed.sh`](scripts/lint/lint-changed.sh)) runs ReSharper InspectCode over the C# files changed in the session **using the exact same scripts, flags, and `.editorconfig` rules as CI** (`scripts/lint/{download-resharper,run-inspectcode,filter-warnings}.sh`, shared with `.github/workflows/test.yml`). Resolve any issues it reports in files you changed before finishing — they are real CI lint findings. If the ReSharper CLI isn't installed it prints how to get it (`bash scripts/lint/download-resharper.sh`) and does not block. It only inspects when `.cs` files changed.

---

## Project Code Standards for Claude Reviews

---

> For expanded patterns and code examples, see the **ecs-system-and-component-design** skill. For async patterns, see the **async-programming** skill. For component cleanup lifecycle, see both the ECS and **sdk-component-implementation** skills.

### 1. **ECS System Rules**

* **Systems are the sole logic entry point for entities.**

  * Entity manipulation **outside systems is strictly forbidden**.
* Systems:

  * **Must not hold persistent entity/component collections.**
  * **Can use temporary collections for per-frame aggregation.**
  * **Must not contain state** — all state goes into ECS.
* Systems must inherit from `BaseUnityLoopSystem`.
* Constructors:

  * Marked `internal`.
  * Accept shared dependencies only (settings, pools, utilities, factories).

### 2. **System Structure and Responsibility**

* **Follow single responsibility principle.**

  * Split systems >200 lines into static counterparts.
  * Group systems into features with a defined execution order.
* Assign each system to a `SystemGroup` based on purpose (e.g. Physics, Presentation).
* Consider creating custom groups for better dependency control.

### 3. **Querying Best Practices**

* Prefer source-generated queries.
* Avoid `World.Query` (last resort due to delegate/closure overhead).
* No nested queries.
* Use `TryGet` for known entities over queries.
* Always filter out `DeleteEntityIntention`.

### 4. **Performance Constraints**

* System `Update()` must be **allocation-free**.
* Keep logic minimal due to multiple world executions per frame.
* Consolidate queries sharing filters.
* Use centralized throttling (`ThrottlingEnabled`) where appropriate.
* Do not use LINQ — it allocates too much memory.

### 5. **Safe Component Mutation**

* Use `ref var` to modify components, **never ****`var`**** alone**.
* **NEVER perform structural changes (Add/Remove that trigger archetype moves) after obtaining a `ref`, `in`, or `out` reference to a component.** Structural changes relocate entity data in memory, invalidating all outstanding `ref`, `in`, and `out` pointers. Components may also hold references to managed objects whose state depends on the current memory layout. Always complete all `ref`/`in`/`out` reads/writes first, then apply structural changes.
* Prefer passing `Entity` by value, not `in Entity`, if modifying.
* Clarify `ref` vs `in` intent; use `ref readonly` for immutable refs.

### 6. **Component Clean-up Patterns**

* Handle cleanup when:

  * Component is removed
  * Entity is destroyed (`DeleteEntityIntention`)
  * World is disposed (via `IFinalizeWorldSystem`)
* Clean-up tasks include:

  * Pool returns
  * Promise invalidation
  * Cache dereferencing
  * Custom logic (e.g., avatar teardown)
* Prefer `ReleasePoolableComponentSystem<T, TProvider>` for pooled disposals.

### 7. **Component Design Principles**

* SDK and custom components treated equally in ECS.
* Add extra data using **separate components**.
* Prefer stateful components over frequent structural changes.
* Use `AssetPromise<T>` for async-loaded assets.
* Always operate `AssetPromise` via `ref`.

### 8. **ECS Singletons**

* Cache singleton components like `Input`, `Player`, `Camera`, `Time`.
* Use `TryGet` for mutation, `Has` for presence checks, `Query` for grouped access.
* Prefer `TryGet` over `Query` for performance (2× faster).

### 9. **Async Flow Guidelines**

* **Minimize detached ****`UniTask`****/****`UniTaskVoid`**** calls.**
* Always catch exceptions:

  * Ignore `OperationCanceledException`
  * Log/report all others via `ReportHub.LogException`
* Use `SuppressToResultAsync()` to simplify exception handling.
* Handle cancellation with `ct.IsCancellationRequested`, never `ThrowIfCancellationRequested()`.

### 10. **Testing Systems**

* Use `UnitySystemTestBase<T>` for world lifecycle in tests.
* Expose system constructors via `[InternalsVisibleTo]`.
* Use NUnit + NSubstitute.

---

### 11. **Anti-Patterns (especially important for AI-authored code)**

Reviewers have repeatedly identified AI-generated code by these smells. Check yourself against this list before submitting.

* **Bridge/wrapper classes on the same abstraction layer.** If class `B` exists only to forward calls to class `A`, with no polymorphism, no second caller, and no test-isolation benefit — **inline it into the caller**. A one-use helper is not a helper.
* **Delegate-wrapped properties passed through layers.** Passing `Func<Config>` when `Config` would do, or wrapping every property of an object in its own `Func<T>`, is obfuscation. **Pass the object.** If you need to capture one changing value (e.g. a `messageId`), store it on the consumer, not as a closure forwarded through three constructors.
* **Extracting when you should merge.** If class `X` does nothing without class `Y`, merging them is usually the right call. Don't split on "SOLID" grounds alone — splits must pay for themselves in polymorphism, reuse, or test seams.
* **Interfaces with one implementation and no test coverage.** Delete the interface. The concrete class is the contract.
* **Per-frame logic inside a presenter/controller.** If you're writing `Tick(float dt)` or polling in a UI class, the work belongs in an `ECS` system (`BaseUnityLoopSystem` or `ControllerECSBridgeSystem`). Camera, time, player, and input are already ECS singletons — reach for `TryGet` in a system, not `Camera.main` in a presenter. Profiler markers come free in systems.
* **Defensive null-checks against non-null declarations.** If the declared type is `T` (not `T?`), don't null-check it. Trust the annotations. Every redundant check is a lie to the reader about what can happen.
* **Debug/mock code in production hot paths.** Runtime bools like `DebugRandomizeX` execute on every call in retail builds. Guard debug branches with `#if UNITY_EDITOR` or move them to an editor-only companion system — never rely on a runtime flag alone.
* **Plugins initializing or mutating containers.** Containers are constructed top-down from the composition root. Plugins **read** from containers. A plugin that writes into a container is a signal the dependency graph is inverted — create a scoped container instead.
* **`ObjectProxy` is an anti-pattern** — never introduce a new instance. The codebase has been swept of it; the only legitimate remaining uses model true runtime lifecycles (`StaticContainer.MainPlayerAvatarBaseProxy` — avatar set/released as the player loads, and `ExposedCameraData.CameraEntityProxy` — entity created during world build). Every other use was a wiring-order mistake and was eliminated by restructuring. To decouple without it, pick the matching recipe from `docs/architecture-overview.md` § "Deferred dependencies — decoupling without ObjectProxy": create the service before its consumers (hoist it out of a UI container into its own container), model an optional feature as a nullable dependency or null-object, let the container that owns a late-created service also construct the plugins that need it (`DynamicWorldContainer.WorldPlugins`), or pass per-scene data through `ECSWorldInstanceSharedDependencies`.
* **Retry/resolve loops without a termination condition.** A loop that re-adds the same unresolved item to the queue will spin forever when the server returns stable but empty results. Always have a "give up" predicate.
* **Wiring pooled/virtualized list items per rebind.** For item pools, wire callbacks once when the item is created, not every time `SetItemData` runs. Prefer an `Action` field (single subscriber, direct assignment) over C# `event` (`+=`/`-=` churn) when there is exactly one subscriber.
* **Reimplementing primitives that already exist.** Before writing manual atlas UV math, check `TMP_Sprite Asset`. Before hand-batching profile lookups, check the batched `GetProfilesAsync(IReadOnlyList<string>, ct)` overload. Before adding a bespoke event pathway, check `ViewEventBus` / `ChatEvents`.
* **Comments that narrate caller/external behavior.** A comment must state only what the annotated code itself does or guarantees ("remove the corrupt file so the next read doesn't hit it"), never what callers or upper layers will do with the result ("so callers treat it as a miss and re-download"). External behavior can change without this code changing, silently turning the comment into a lie.

### Other project-specific rules

* Use `ReportHub` instead of `Debug.Log` for all logging.
* Minimize GC pressure: reuse objects, use object pooling, avoid boxing/unboxing, use `StringBuilder` for string concatenation.
