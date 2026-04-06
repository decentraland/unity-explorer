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
