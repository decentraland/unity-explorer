---
name: ecs-system-and-component-design
description: "ECS system and component design using Arch ECS. Use when creating or modifying systems, components, queries, cleanup, singletons, or tests -- also for code reviewing World/Entity/component usage."
user-invocable: false
---

# ECS System & Component Design

## Sources

- `docs/development-guide.md` -- Primary ECS development reference
- `docs/architecture-overview.md` -- High-level ECS architecture and patterns
- `docs/systems.md` -- Scene bounds and rendering system details
- `CLAUDE.md` -- Condensed ECS rules

---

## System Design Rules

> Condensed rules are in CLAUDE.md sections 1-7. This skill provides expanded patterns and guidance not covered there. For a full clean system code example, see [reference.md](reference.md).

Key reminders:
- Systems inherit from `BaseUnityLoopSystem`, constructors are `internal`, accept shared dependencies only
- Systems must not hold persistent entity/component collections or contain state
- `Update()` must be allocation-free; no LINQ
- Split systems >200 lines into static counterparts
- Assign each system to a `SystemGroup`; use `[UpdateBefore]`/`[UpdateAfter]` for ordering

---

## Querying Best Practices

Ranked from most preferred to least:

1. **Source-generated queries** (preferred) -- Use `[Query]` attribute with `[Data]` parameters
2. **`GetChunkIterator()`** -- For generic systems
3. **`World.InlineQuery`** -- Outside systems or generic cases; uses `IForEach<T>` struct
4. **`World.Query`** -- Last resort due to delegate/closure overhead

Always filter out `DeleteEntityIntention`. No nested queries. Use `TryGet` for known entities over queries.

---

## Safe Component Mutation

```csharp
// CORRECT -- ref var modifies the component in-place
ref var component = ref world.Get<MyComponent>(entity);
component.Value = 42;

// WRONG -- var copies the component; changes are lost
var component = world.Get<MyComponent>(entity);
component.Value = 42; // This modification is lost!
```

- **NEVER perform structural changes (Add/Remove) after obtaining a `ref`, `in`, or `out` reference.** Structural changes relocate entity data in memory, invalidating all outstanding pointers.
- Components should be data-only (no logic); may have pool, static factory, or non-empty constructor
- Prefer `struct` for components; use `class` for MonoBehaviors, existing class lifecycle, or cross-world references
- Clarify `ref` vs `in` intent; use `ref readonly` for immutable refs

---

## Component Cleanup Patterns

Cleanup must handle three triggers:

### 1. Component Removed

```csharp
[Query] [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
private void HandleComponentRemoval(ref AudioSourceComponent component)
    => releaseAudioSourceComponent.Update(ref component);
```

### 2. Entity Destroyed

```csharp
[Query] [All(typeof(DeleteEntityIntention))]
private void HandleEntityDestruction(ref AudioSourceComponent component)
    => releaseAudioSourceComponent.Update(ref component);
```

### 3. World Disposed

```csharp
public void FinalizeComponents(in Query query)
{
    World.InlineQuery<ReleaseAudioSourceComponent, AudioSourceComponent>(
        in new QueryDescription().WithAll<AudioSourceComponent>(),
        ref releaseAudioSourceComponent);
}
```

**Cleanup tasks include:** pool returns, promise invalidation/dereferencing, cache dereferencing, custom logic (e.g., avatar teardown).

Prefer `ReleasePoolableComponentSystem<T, TProvider>` for pooled disposals. For the full cleanup lifecycle code example with `IForEach<T>` struct pattern, see [reference.md](reference.md).

---

## Intention Components (Request-Response Pattern)

The codebase uses "intention" struct components to model async ECS requests. A system creates an entity with an intention; a loading system processes it and adds `StreamableLoadingResult<T>`.

**4-step pattern:**
1. **Create** -- Build an `AssetPromise` entity with the intention component
2. **Load** -- A `LoadSystemBase<TAsset, TIntention>` picks it up, loads the asset, adds `StreamableLoadingResult<T>`
3. **Consume** -- The requesting system calls `TryConsume` / `TryGetResult` to retrieve the result
4. **Cleanup** -- Dereference via `TryDereference`, destroy via `ForgetLoading` or `Consume`

**Base interfaces:** `IAssetIntention` (has `CancellationTokenSource`), `ILoadingIntention : IAssetIntention` (adds `CommonLoadingArguments`).

**Common intention types:** `GetTextureIntention`, `GetAudioClipIntention`, `GetSceneFacadeIntention`, `DeleteEntityIntention`, `GetProfilesBatchIntent`, and 15+ others.

See the **asset-promise-lifecycle** skill for full creation, polling, error handling, and cleanup patterns.

---

## ECS Singletons

> See CLAUDE.md section 8 for core singleton rules.

- Cache via `WorldExtensions` (`world.CacheCamera()`, `world.CachePlayer()`)
- Use `SingleInstanceEntity` struct; cache once, store in system field
- Use `TryGet` for mutation, `Has` for presence checks, `Query` for grouped access (prefer `TryGet` -- 2x faster)

---

## Testing Systems

- Use `UnitySystemTestBase<T>` for world lifecycle management
- Expose system constructors via `[InternalsVisibleTo]`
- Use NSubstitute for mocking dependencies
- Test multiple scenarios: creation, update (dirty flag), cancellation, cleanup

See the **testing-infrastructure** skill for full test patterns, utilities, and examples.

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **testing-infrastructure** -- `UnitySystemTestBase<T>`, test utilities, mocking strategies
- **asset-promise-lifecycle** -- `AssetPromise` creation, polling, consumption, and cleanup
- **plugin-architecture** -- System registration via `InjectToWorld`, plugin lifecycle
