# Avatar Rendering Pipeline -- Detailed Reference

## GPU Skinning Code

### Stage 1 -- CPU Bone Matrix Job

`StartAvatarMatricesCalculationSystem` schedules a Burst-compiled Job early in the frame:

```csharp
protected override void Update(float t)
{
    ExecuteQuery(World);  // collects avatar bone data
    avatarTransformMatrixBatchJob.ScheduleBoneMatrixCalculation();
}
```

### Stage 2 -- Job Completion + GPU Transfer

`FinishAvatarMatricesCalculationSystem` runs in `PreRenderingSystemGroup` to maximize Job parallelism:

```csharp
protected override void Update(float t)
{
    jobWrapper.CompleteBoneMatrixCalculations();
    currentResult = jobWrapper.job.BonesMatricesResult;  // NativeArray<float4x4>
    ExecuteQuery(World);
}

[Query] [All(typeof(AvatarShapeComponent))] [None(typeof(DeleteEntityIntention))]
private void Execute(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
    ref AvatarCustomSkinningComponent computeShaderSkinning)
{
    Result result = computeShaderSkinning.ComputeSkinning(
        currentResult, avatarTransformMatrixComponent.IndexInGlobalJobArray);
}
```

### Stage 3 -- Compute Shader

`ComputeSkinning` calls `SetData` to upload bone matrices to GPU, then dispatches the compute shader. The shader calculates positions, normals, and tangents using 4-weight bone skinning. Results are written into the Global Vertex Buffer.

---

## GVB Defragmentation Code

`MakeVertsOutBufferDefragmentationSystem` triggers when free regions exceed the threshold. It compacts rented regions and remaps avatar indices:

```csharp
protected override void Update(float t)
{
    IReadOnlyDictionary<int, Slice> defragmentationMap = computeBufferHandler.TryMakeDefragmentation();
    if (defragmentationMap.Count == 0) return;
    UpdateIndicesQuery(World, defragmentationMap);
}

[Query]
private void UpdateIndices([Data] IReadOnlyDictionary<int, Slice> remapping,
    ref AvatarCustomSkinningComponent avatarCustomSkinningComponent)
{
    if (remapping.TryGetValue(avatarCustomSkinningComponent.VertsOutRegion.StartIndex, out Slice newRegion))
        avatarCustomSkinningComponent.SetVertOutRegion(newRegion);
}
```

---

## SMR to MeshRenderer Conversion Code

Wearables arrive as `SkinnedMeshRenderer` from Asset Bundles. `ComputeShaderSkinning` converts them to `MeshRenderer` for custom GPU skinning:

```csharp
private (MeshRenderer, MeshFilter) SetupMesh(SkinnedMeshRenderer skin)
{
    GameObject go = skin.gameObject;
    MeshFilter filter = go.AddComponent<MeshFilter>();
    filter.mesh = skin.sharedMesh;
    MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
    meshRenderer.renderingLayerMask = 2;
    Object.Destroy(skin);
    return (meshRenderer, filter);
}
```

---

## Facial Features Code

Facial features (eyes, eyebrows, mouth) use suffix-based matching on the renderer name:

```csharp
private static readonly (string suffix, string category, int defaultSlotIndexUsed, ...)[] SUFFIX_CATEGORY_MAP =
{
    ("Mask_Eyes",     WearableCategories.Categories.EYES,     1, shape => shape.EyesColor),
    ("Mask_Eyebrows", WearableCategories.Categories.EYEBROWS, 0, shape => shape.HairColor),
    ("Mask_Mouth",    WearableCategories.Categories.MOUTH,    0, shape => shape.SkinColor),
};
```

---

## Color Assignment Code

Original material names determine avatar color application:

```csharp
if (name.Contains("skin", StringComparison.OrdinalIgnoreCase))
    avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.SkinColor);
else if (name.Contains("hair", StringComparison.OrdinalIgnoreCase))
    avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.HairColor);
```

---

## Texture Arrays Code

Textures are copied into shared `Texture2DArray` objects to reduce draw call binding cost. `TextureArrayContainer` manages slots per shader, with resolutions at 256 and 512:

```csharp
TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer
    .SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
```

---

## EmotePlayer Code

```csharp
public class EmotePlayer
{
    private readonly Dictionary<GameObject, GameObjectPool<EmoteReferences>> pools = new();
    private readonly Dictionary<EmoteReferences, GameObjectPool<EmoteReferences>> emotesInUse = new();

    public bool Play(GameObject mainAsset, AudioClip? audioAsset, bool isLooping,
        bool isSpatial, in IAvatarView view, ref CharacterEmoteComponent emoteComponent)
    {
        // Get or create pool for this emote asset
        if (!pools.ContainsKey(mainAsset))
            pools.Add(mainAsset, new GameObjectPool<EmoteReferences>(poolRoot,
                () => CreateNewEmoteReference(mainAsset), onRelease: releaseEmoteReferences));

        EmoteReferences emoteReferences = pools[mainAsset].Get();
        // Parent to avatar, play Mecanim or legacy animation
        // ...
    }

    public void Stop(EmoteReferences emoteReference)
    {
        if (!emotesInUse.Remove(emoteReference, out var pool)) return;
        pool.Release(emoteReference);
    }
}
```

---

## ReleaseAvatar Cleanup Code

```csharp
public static void Execute(FixedComputeBufferHandler vertOutBuffer, IAttachmentsAssetsCache wearableAssetsCache,
    IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<ComputeShader> computeShaderSkinningPool,
    in AvatarShapeComponent avatarShapeComponent, ref AvatarCustomSkinningComponent skinningComponent,
    ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, AvatarTransformMatrixJobWrapper jobWrapper)
{
    vertOutBuffer.Release(skinningComponent.VertsOutRegion);    // GVB region release
    skinningComponent.Dispose(avatarMaterialPoolHandler, computeShaderSkinningPool); // material + compute shader pool return
    jobWrapper.ReleaseAvatar(ref avatarTransformMatrixComponent); // Job array slot release
    wearableAssetsCache.ReleaseAssets(avatarShapeComponent.InstantiatedWearables); // wearable cache deref
}
```
