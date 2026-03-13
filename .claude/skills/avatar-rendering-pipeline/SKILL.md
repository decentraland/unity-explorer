---
name: avatar-rendering-pipeline
description: "Avatar rendering pipeline — GPU skinning, compute shaders, Global Vertex Buffer, wearable loading, material pooling, and emote system. Use when modifying avatar instantiation, skinning systems, GVB defragmentation, wearable material setup, texture array slots, emote integration, or avatar cleanup lifecycle."
user-invocable: false
---

# Avatar Rendering Pipeline

## Sources

- `docs/avatar-rendering.md` — GPU skinning, compute shaders, Global Vertex Buffer, AvatarCelShading shader
- `docs/emotes.md` — Emote loading, Mecanim controllers, EmotePlayer pooling

---

## System Execution Order

`AvatarGroup` runs inside `PresentationSystemGroup`. Systems execute in this order:

```
AvatarGroup (PresentationSystemGroup)
  1. AvatarLoaderSystem           — creates AvatarShapeComponent + WearablePromise from Profile/PBAvatarShape
  2. MakeVertsOutBufferDefragmentationSystem  — GVB defrag (runs before instantiation)
  3. AvatarInstantiatorSystem     — consumes WearablePromise, instantiates wearables, allocates GVB region
  4. StartAvatarMatricesCalculationSystem     — schedules bone matrix Job (after instantiation)

FinishAvatarMatricesCalculationSystem (PreRenderingSystemGroup)
  5. Completes bone Job, dispatches compute shader skinning

AvatarShapeVisibilitySystem (CameraGroup)
  6. Frustum culling, dithering, blocked/banned user hiding

AvatarCleanUpSystem (CleanUpGroup)
  7. Releases GVB region, pools, materials on DeleteEntityIntention
```

Ordering is enforced via `[UpdateBefore]`/`[UpdateAfter]` attributes:

```csharp
[UpdateInGroup(typeof(AvatarGroup))]
[UpdateBefore(typeof(AvatarInstantiatorSystem))]
public partial class MakeVertsOutBufferDefragmentationSystem : BaseUnityLoopSystem { ... }

[UpdateInGroup(typeof(AvatarGroup))]
[UpdateAfter(typeof(AvatarLoaderSystem))]
public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem { ... }

[UpdateInGroup(typeof(AvatarGroup))]
[UpdateAfter(typeof(AvatarInstantiatorSystem))]
public partial class StartAvatarMatricesCalculationSystem : BaseUnityLoopSystem { ... }
```

---

## GPU Skinning Pipeline

Three-stage pipeline: CPU Job (bone matrices) -> GPU transfer -> Compute Shader (vertex transform).

### Stage 1 — CPU Bone Matrix Job

`StartAvatarMatricesCalculationSystem` schedules a Burst-compiled Job early in the frame:

```csharp
protected override void Update(float t)
{
    ExecuteQuery(World);  // collects avatar bone data
    avatarTransformMatrixBatchJob.ScheduleBoneMatrixCalculation();
}
```

### Stage 2 — Job Completion + GPU Transfer

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

### Stage 3 — Compute Shader

`ComputeSkinning` calls `SetData` to upload bone matrices to GPU, then dispatches the compute shader. The shader calculates positions, normals, and tangents using 4-weight bone skinning. Results are written into the Global Vertex Buffer.

---

## Global Vertex Buffer (GVB)

All avatar vertex data lives in a single `ComputeBuffer` set as a global shader buffer. The `AvatarCelShading` shader indexes into it per-material.

### FixedComputeBufferHandler

Manages rent/release of `Slice` regions within the fixed-size buffer:

```csharp
public class FixedComputeBufferHandler : IDisposable
{
    private const int DEFRAGMENTATION_THRESHOLD = 20;
    public readonly ComputeBuffer Buffer;

    public Slice Rent(int length)    // finds first-fit free region
    public void Release(Slice slice) // returns region, merges adjacent free regions
    public IReadOnlyDictionary<int, Slice> TryMakeDefragmentation() // compacts when free regions >= threshold
}
```

The buffer is created in `AvatarPlugin.InjectToWorld` and bound globally:

```csharp
var vertOutBuffer = new FixedComputeBufferHandler(5_000_000, Unsafe.SizeOf<CustomSkinningVertexInfo>());
Shader.SetGlobalBuffer(GLOBAL_AVATAR_BUFFER, vertOutBuffer.Buffer);
```

### Defragmentation

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

## Wearable Loading

### Flow

1. `AvatarLoaderSystem` creates a `WearablePromise` from `Profile` or `PBAvatarShape`
2. Loading systems resolve the promise (Asset Bundle download + wearable resolution)
3. `AvatarInstantiatorSystem` consumes the promise and instantiates wearables

### SMR to MeshRenderer Conversion

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

### Facial Features

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

## Material Pooling & Shader Conventions

### Shader Categories

Materials are pooled by shader type using `AvatarMaterialPoolHandler`:

| Shader ID | Shader Name | Usage |
|-----------|-------------|-------|
| `SHADERID_DCL_TOON` (2) | `DCL/DCL_Toon` | Regular wearable materials |
| `SHADERID_DCL_FACIAL_FEATURES` (3) | `DCL/DCL_Avatar_Facial_Features` | Eyes, eyebrows, mouth |

### Color Assignment

Original material names determine avatar color application:

```csharp
if (name.Contains("skin", StringComparison.OrdinalIgnoreCase))
    avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.SkinColor);
else if (name.Contains("hair", StringComparison.OrdinalIgnoreCase))
    avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.HairColor);
```

### Texture Arrays

Textures are copied into shared `Texture2DArray` objects to reduce draw call binding cost. `TextureArrayContainer` manages slots per shader, with resolutions at 256 and 512. Each material gets a `TextureArraySlot?[]` tracking its occupied slots.

```csharp
TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer
    .SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
```

---

## Emote System

### EmotePlayer

`EmotePlayer` manages pooled emote instances. Multiple avatars can share the same emote asset; each gets a pooled `EmoteReferences` instance:

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

### Intent-Based Loading

Entities receive a `CharacterEmoteIntent` component. `CharacterEmoteSystem` consumes the intent — if the emote is not yet loaded, the intent persists until the asset resolves.

### Mecanim vs Legacy

Asset Bundle emotes contain an `AnimatorController` with trigger-based transitions. The avatar clip is assigned via `view.ReplaceEmoteAnimation()`. Legacy animations (local scene dev only) use `Animation.Play()`.

Clip extraction follows naming conventions: `_avatar` suffix for avatar clips, `_prop` suffix for prop clips.

---

## Cleanup

`AvatarCleanUpSystem` handles `DeleteEntityIntention` and world disposal. Cleanup delegates to `ReleaseAvatar.Execute`:

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

The `AvatarBase` GameObject is returned to its pool separately after `ReleaseAvatar.Execute`.

For cleanup patterns (component removed, entity destroyed, world disposed), see the **ecs-system-and-component-design** skill.

---

## Cross-References

- **ecs-system-and-component-design** — Cleanup lifecycle patterns (component removal, entity destruction, world disposal)
- **asset-promise-lifecycle** — `WearablePromise` creation, polling, consumption, and dereferencing in `AvatarLoaderSystem`
- **plugin-architecture** — `AvatarPlugin` as `IDCLGlobalPlugin`, system injection via `InjectToWorld`, pool initialization
