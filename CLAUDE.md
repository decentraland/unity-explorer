# CLAUDE.md

## Startup

At the start of every conversation, read [`docs/SKILL.md`](docs/SKILL.md) to load the project documentation map. Skills are automatically loaded by Claude Code from `.claude/skills/` — do not manually read them.

Before writing or modifying any code, follow the code-standards skill for naming conventions, member ordering, formatting rules, and test patterns. For edge cases, [`Explorer/.editorconfig`](Explorer/.editorconfig) is the authoritative formatting reference.

---

## Project Code Standards for Claude Reviews

---

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

### Specific Notes

* `ObjectProxy` was introduced to **resolve circular dependencies**. While effective, **consider this an anti-pattern**. Favor clearer dependency injection.
* Interfaces or abstract classes with only one implementation and no test coverage **should be avoided or merged**.
* Use `ReportHub` instead of `Debug.Log` for all logging.
* Minimize GC pressure: reuse objects, use object pooling, avoid boxing/unboxing, use `StringBuilder` for string concatenation.
