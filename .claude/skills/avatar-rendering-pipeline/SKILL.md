---
name: avatar-rendering-pipeline
description: "Avatar rendering -- GPU skinning, compute shaders, GVB, wearable loading, material pooling, emotes. Use when modifying avatar instantiation, skinning, wearable materials, texture arrays, or emote integration."
user-invocable: false
---

# Avatar Rendering Pipeline

## Sources

- `docs/avatar-rendering.md` -- GPU skinning, compute shaders, Global Vertex Buffer, AvatarCelShading shader
- `docs/emotes.md` -- Emote loading, Mecanim controllers, EmotePlayer pooling

---

## System Execution Order

`AvatarGroup` runs inside `PresentationSystemGroup`. Systems execute in this order:

```
AvatarGroup (PresentationSystemGroup)
  1. AvatarLoaderSystem           -- creates AvatarShapeComponent + WearablePromise from Profile/PBAvatarShape
  2. MakeVertsOutBufferDefragmentationSystem  -- GVB defrag (runs before instantiation)
  3. AvatarInstantiatorSystem     -- consumes WearablePromise, instantiates wearables, allocates GVB region
  4. StartAvatarMatricesCalculationSystem     -- schedules bone matrix Job (after instantiation)

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

- **Stage 1 -- CPU Bone Matrix Job:** `StartAvatarMatricesCalculationSystem` schedules a Burst-compiled Job early in the frame via `avatarTransformMatrixBatchJob.ScheduleBoneMatrixCalculation()`.
- **Stage 2 -- Job Completion + GPU Transfer:** `FinishAvatarMatricesCalculationSystem` runs in `PreRenderingSystemGroup` to maximize Job parallelism. Completes the Job, retrieves `NativeArray<float4x4>`, then calls `ComputeSkinning` per avatar.
- **Stage 3 -- Compute Shader:** `ComputeSkinning` uploads bone matrices to GPU via `SetData`, dispatches the compute shader for 4-weight bone skinning. Results are written into the Global Vertex Buffer.

---

## Global Vertex Buffer (GVB)

All avatar vertex data lives in a single `ComputeBuffer` set as a global shader buffer. The `AvatarCelShading` shader indexes into it per-material.

### FixedComputeBufferHandler API

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

Buffer is created in `AvatarPlugin.InjectToWorld` (5M vertices) and bound globally via `Shader.SetGlobalBuffer`.

**Defragmentation:** `MakeVertsOutBufferDefragmentationSystem` triggers when free regions exceed the threshold, compacts rented regions, and remaps avatar indices via query.

---

## Wearable Loading

### 3-Step Flow

1. `AvatarLoaderSystem` creates a `WearablePromise` from `Profile` or `PBAvatarShape`
2. Loading systems resolve the promise (Asset Bundle download + wearable resolution)
3. `AvatarInstantiatorSystem` consumes the promise and instantiates wearables

Wearables arrive as `SkinnedMeshRenderer` from Asset Bundles; `ComputeShaderSkinning` converts them to `MeshRenderer` for custom GPU skinning. Facial features (eyes, eyebrows, mouth) use suffix-based matching on the renderer name (`Mask_Eyes`, `Mask_Eyebrows`, `Mask_Mouth`).

---

## Material Pooling & Shader Conventions

Materials are pooled by shader type using `AvatarMaterialPoolHandler`:

| Shader ID | Shader Name | Usage |
|-----------|-------------|-------|
| `SHADERID_DCL_TOON` (2) | `DCL/DCL_Toon` | Regular wearable materials |
| `SHADERID_DCL_FACIAL_FEATURES` (3) | `DCL/DCL_Avatar_Facial_Features` | Eyes, eyebrows, mouth |

Color assignment is based on original material names: `skin` -> SkinColor, `hair` -> HairColor. Textures are copied into shared `Texture2DArray` objects (256/512 resolution) to reduce draw call binding cost via `TextureArrayContainer`.

---

## Emote System

`EmotePlayer` manages pooled emote instances. Multiple avatars can share the same emote asset; each gets a pooled `EmoteReferences` instance via `GameObjectPool<EmoteReferences>`.

- **Intent-Based Loading:** Entities receive `CharacterEmoteIntent`. `CharacterEmoteSystem` consumes the intent; if the emote is not yet loaded, the intent persists until the asset resolves.
- **Mecanim vs Legacy:** Asset Bundle emotes use `AnimatorController` with trigger-based transitions. Clip extraction follows naming: `_avatar` suffix for avatar clips, `_prop` suffix for prop clips. Legacy animations (local scene dev only) use `Animation.Play()`.

---

## Cleanup

`AvatarCleanUpSystem` handles `DeleteEntityIntention` and world disposal. Cleanup delegates to `ReleaseAvatar.Execute` which performs: GVB region release, material + compute shader pool return, Job array slot release, and wearable cache deref. The `AvatarBase` GameObject is returned to its pool separately.

For cleanup patterns (component removed, entity destroyed, world disposed), see the **ecs-system-and-component-design** skill.

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **ecs-system-and-component-design** -- Cleanup lifecycle patterns (component removal, entity destruction, world disposal)
- **asset-promise-lifecycle** -- `WearablePromise` creation, polling, consumption, and dereferencing in `AvatarLoaderSystem`
- **plugin-architecture** -- `AvatarPlugin` as `IDCLGlobalPlugin`, system injection via `InjectToWorld`, pool initialization
