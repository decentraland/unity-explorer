# Asset Promise Lifecycle

## Activation

Use this skill when loading assets asynchronously through ECS — textures, models, audio clips, wearables, or any resource that requires an async loading pipeline.

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

## Code Example — Full Promise Lifecycle

From `MapPinLoaderSystem.cs`:

```csharp
[UpdateInGroup(typeof(ComponentInstantiationGroup))]
public partial class MapPinLoaderSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    private const int ATTEMPTS_COUNT = 6;

    protected override void Update(float t)
    {
        LoadMapPinQuery(World);
        UpdateMapPinQuery(World);
        HandleComponentRemovalQuery(World);
        HandleEntityDestructionQuery(World);

        if (useCustomMapPinIcons)
            ResolveTexturePromiseQuery(World);
    }

    // CREATE — Build a texture promise when the map pin has a texture
    private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent,
        ref Promise? promise)
    {
        if (textureComponent == null)
            return false;

        TextureComponent textureComponentValue = textureComponent.Value;

        // Skip if same texture already requested
        if (TextureComponentUtils.Equals(ref textureComponentValue, ref promise))
            return false;

        // Dereference old promise before creating new one
        DereferenceTexture(ref promise);

        promise = Promise.Create(
            World,
            new GetTextureIntention(
                textureComponentValue.Src,
                textureComponentValue.FileHash,
                textureComponentValue.WrapMode,
                textureComponentValue.FilterMode,
                textureComponentValue.TextureType,
                attemptsCount: ATTEMPTS_COUNT,
                reportSource: nameof(MapPinLoaderSystem)),
            partitionComponent);

        return true;
    }

    // POLL + CONSUME — Check if the loading system has completed
    [Query]
    private void ResolveTexturePromise(in Entity entity, ref MapPinComponent mapPinComponent)
    {
        if (mapPinComponent.TexturePromise is null ||
            mapPinComponent.TexturePromise.Value.IsConsumed)
            return;

        if (mapPinComponent.TexturePromise.Value.TryConsume(World,
            out StreamableLoadingResult<TextureData> texture))
        {
            mapPinComponent.TexturePromise = null;
            mapPinsEventBus.UpdateMapPinThumbnail(entity, texture.Asset!.EnsureTexture2D());
        }
    }

    // CLEANUP — Dereference in all cleanup paths
    [Query]
    [None(typeof(PBMapPin), typeof(DeleteEntityIntention))]
    private void HandleComponentRemoval(in Entity entity, ref MapPinComponent mapPinComponent)
    {
        DereferenceTexture(ref mapPinComponent.TexturePromise);
        World.Remove<MapPinComponent>(entity);
        mapPinsEventBus.RemoveMapPin(entity);
    }

    [Query]
    [All(typeof(DeleteEntityIntention))]
    private void HandleEntityDestruction(in Entity entity, ref MapPinComponent mapPinComponent)
    {
        DereferenceTexture(ref mapPinComponent.TexturePromise);
        mapPinsEventBus.RemoveMapPin(entity);
    }

    // DEREFERENCE — Allow memory reclamation
    private void DereferenceTexture(ref Promise? promise)
    {
        if (promise == null)
            return;

        Promise promiseValue = promise.Value;
        promiseValue.TryDereference(World);
    }

    public void FinalizeComponents(in Query query)
    {
        CleanupOnFinalizeQuery(World);
    }
}
```

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

- `TexturesCache` → `Texture2D`
- `WearableAssetsCache` → `CachedWearable` → `WearableAsset` → `AssetBundleData`
- `GltfContainerAssetsCache` → `GltfContainerAsset` → `AssetBundleData`
