# Asset Promise Lifecycle — Detailed Reference

## Full Promise Lifecycle — MapPinLoaderSystem

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

## Ref-Counted Shared-Asset Lifecycle — Raw GLTF

The `MapPinLoaderSystem` example above is the simple shape: one consumer per intention, one Texture2D per consumer, no dedup. Some loading paths use a **ref-counted shared-asset cache** (`RefCountStreamableCacheBase<TAssetData, TAsset, TIntention>`) so multiple consumers requesting the same content share a single underlying asset and a single in-flight load. Raw GLTF loading is the canonical example.

### Pipeline shape

```
LoadGLTFSystem.FlowInternalAsync                    (cache miss path)
  ├── new GltfImport()
  ├── await gltfImport.Load()                        ← one parse, one set of Mesh/Material/Texture allocations
  ├── new GLTFData(gltfImport, rootContainer)        ← wraps GltfImport + the instantiated root template
  └── return StreamableLoadingResult<GLTFData>

LoadSystemBase.CacheableFlowAsync
  └── genericCache.PutAsync(intention, gltfData)     ← stored in GltfLoadCache (refCount=0)

ApplyLoadedResult per consumer
  └── cache.AddReference(intention, gltfData)        ← refCount = N consumers; piggy-backers join via OngoingRequests

CreateGltfAssetFromRawGltfSystem.CreateGltfObject    (per consumer)
  └── Object.Instantiate(gltfData.Root)              ← per-consumer GameObject clone; mesh/material/texture refs shared

Per-consumer disposal
  └── GltfContainerAsset.Dispose
        ├── AssetData.Dereference()                  ← refCount--
        └── SafeDestroy(cloneRoot)                   ← clone hierarchy only; underlying assets untouched

Terminal disposal (cache.Unload finds entry with refCount==0)
  └── GLTFData.Dispose → DestroyObject
        ├── gltfImport.Dispose()                     ← textures, materials, meshes destroyed here
        └── SafeDestroy(template Root)
```

### Three rules unique to this shape

**1. Consumer disposal calls `Dereference`, not `Dispose`.**

```csharp
// GltfContainerAsset.Dispose — correct shape
public void Dispose()
{
    AssetData?.Dereference();   // drop this consumer's reference
    AssetData = null;
    SafeDestroy(Root);          // clone hierarchy only
}
```

Calling `Dispose()` from the consumer double-disposes: `PutAsync` already stored the asset in the cache, so the next `cache.Unload` would dispose it again. Counters go negative, `GltfImport.Dispose()` runs twice, `SafeDestroy` runs on an already-destroyed Root.

**2. Cancellation paths still drop the reference.**

```csharp
// CreateGltfAssetFromRawGltfSystem cancellation branch — correct shape
if (assetIntention.CancellationTokenSource.IsCancellationRequested)
{
    if (gltfDataResult.Succeeded && gltfDataResult.Asset is { } gltfData)
        gltfData.Dereference();   // not Dispose() — terminal disposal is owned by the cache

    World.Destroy(entity);
    return;
}
```

The asset has reached the cache via `PutAsync` and `ApplyLoadedResult` has bumped its refcount; cancelling here just means "this consumer no longer wants its reference."

**3. `DisposeAbandonedResult` covers the catch-path window only.**

```csharp
// LoadGLTFSystem override — correct shape
protected override void DisposeAbandonedResult(GLTFData asset)
{
    if (asset.CanBeDisposed())   // refCount == 0 — no consumer claimed it
        asset.Dispose();
}
```

`CacheableFlowAsync` invokes this when a successful result is built but the consumer side cancels before `ApplyLoadedResult` runs (the asset never reached the cache). If any consumer has already incremented the refcount, leave teardown to them and to the cache's `Unload`.

### Rules that fall out of using this shape

- **Intention equality must be consistent.** `RefCountStreamableCacheBase.cache` is a `Dictionary<TIntention, TAssetData>`. Two intentions that should be "the same load" must produce the same hash code, otherwise dedup silently misses. See `GetGLTFIntention.Equals` / `GetHashCode`.

- **Per-consumer cloning is required when consumers reparent.** `CreateGltfAssetFromRawGltfSystem.CreateGltfObject` clones `gltfData.Root` because `FinalizeGltfContainerLoadingSystem` reparents the result to each entity — without cloning, N entities would all reparent the same Root and only the last would render correctly.

- **In LSD, `ECSReloadScene` drains the cache eagerly on `/reload`.** The dev server hashes by path, not content, so cached entries become stale after a file edit. The drain forces fresh loads; production scene transitions don't run it.
