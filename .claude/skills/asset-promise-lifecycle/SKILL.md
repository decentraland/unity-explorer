---
name: asset-promise-lifecycle
description: "AssetPromise lifecycle for async ECS asset loading — textures, models, audio, wearables. Use when creating, polling, consuming, or cleaning up asset promises, or working with memory budgeting."
user-invocable: false
---

# Asset Promise Lifecycle

## Sources

- `docs/asset-promises.md` — Detailed AssetPromise system explanation
- `docs/memory-budgeting-and-resource-unloading.md` — Memory management and cache unloading

---

## AssetPromise Anatomy

`AssetPromise<TAsset, TLoadingIntention>` is a lightweight struct handle to an ECS entity representing a loading operation.

**Type aliases** improve readability:

```csharp
using Promise = ECS.StreamableLoading.Common.AssetPromise<
    ECS.StreamableLoading.Textures.TextureData,
    ECS.StreamableLoading.Textures.GetTextureIntention>;
```

## Lifecycle

### 1. Creation

```csharp
// Create a promise entity with a loading intention
promise = Promise.Create(
    World,
    new GetTextureIntention(src, fileHash, wrapMode, filterMode, textureType,
        attemptsCount: 6, reportSource: nameof(MapPinLoaderSystem)),
    partitionComponent
);

// Or create an already-resolved promise
promise = Promise.CreateFinalized(World, existingAsset);
```

### 2. Polling / Retrieval

```csharp
// Non-destructive peek — check if done without consuming
if (promise.TryGetResult(World, out StreamableLoadingResult<TextureData> result))
{
    // Result available, promise entity still alive
}

// Destructive consume — gets result AND destroys the promise entity (one-time only)
if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
{
    // Result consumed, promise entity destroyed
    Texture2D texture = result.Asset!.EnsureTexture2D();
}

// Safe consume — like TryConsume but handles missing entities gracefully
if (promise.SafeTryConsume(World, out StreamableLoadingResult<TextureData> result))
{
    // ...
}
```

### 3. Async Usage

```csharp
// Await + consume (destroys entity on completion)
TextureData data = await promise.ToUniTaskAsync(World, ct);

// Await without consuming (entity stays alive)
TextureData data = await promise.ToUniTaskWithoutDestroyAsync(World, ct);
```

### 4. Cleanup

```csharp
// Cancel loading + destroy entity
promise.ForgetLoading(World);

// Just destroy entity (if already consumed or no longer needed)
promise.Consume(World);
```

### 5. Cache Dereferencing

When cleaning up, always dereference cached assets to allow memory reclamation:

```csharp
promise.TryDereference(World);
```

## Error Handling

`StreamableLoadingResult<T>` carries success/failure state:

- `.Succeeded` — `true` if asset loaded successfully
- `.Asset` — the loaded asset (null on failure)
- `.Exception` — the failure exception (null on success)

```csharp
if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
{
    if (result.Succeeded)
    {
        Texture2D texture = result.Asset!.EnsureTexture2D();
        ApplyTexture(texture);
    }
    else
    {
        // Log the exception if it wasn't already logged by StreamableLoadingException
        result.TryLogException();
        ApplyFallback();
    }
}
```

`TryLogException()` logs the exception via `ReportHub` only if it wasn't already logged during construction (i.e., non-`StreamableLoadingException` failures). Use it for deferred logging in consuming systems.

---

## Memory Budgeting

### MemoryBudgetProvider

Implements `IConcurrentBudgetProvider`. Uses `IProfilingProvider` for current memory usage and `ISystemMemory` for total.

- `TrySpendBudget()` returns `false` when memory reaches the Full threshold
- Loading systems (`CreateGltfAssetFromAssetBundleSystem`, `DeferredLoadingSystem`) halt when budget unavailable

### Resource Unloading Pipeline

1. `ReleaseMemorySystem` monitors memory via `MemoryBudgetProvider`
2. `CacheCleaner` (visitor pattern) unloads registered pools/caches
3. Caches use LRU-based `PriorityQueue` for eviction; pools use chunk-based clearance within frame-time budget

### Cache Dereferencing Chain

Always dereference in cleanup paths to allow the unloading pipeline to reclaim memory:

- `TexturesCache` -> `Texture2D`
- `WearableAssetsCache` -> `CachedWearable` -> `WearableAsset` -> `AssetBundleData`
- `GltfContainerAssetsCache` -> `GltfContainerAsset` -> `AssetBundleData`

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **ecs-system-and-component-design** — Component cleanup lifecycle, `IFinalizeWorldSystem`
- **plugin-architecture** — `IAssetsProvisioner`, `ProvidedAsset<T>`, pool registration via `IComponentPoolsRegistry`
