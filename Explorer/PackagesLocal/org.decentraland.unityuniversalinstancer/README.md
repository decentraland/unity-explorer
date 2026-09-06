# Unity Universal Instancer (org.decentraland.unityuniversalinstancer)

GPU-driven instancing runtime for the Decentraland landscape's tree, rock and
detail prototypes. It provides the `GPUInstancerPro` C# surface the client's
call sites bind to — `GPUICoreAPI`, `GPUIProfile`, `GPUIRuntimeSettings`,
`GPUIShaderBindings`, `GPUIDebuggerCanvas`, `GPUIPrefabManager`,
`GPUIDetailManager`, `GPUITreeManager` — and implements it with a Burst cull +
LOD-selection job that feeds one flat per-prototype `GraphicsBuffer` upload
and `Graphics.RenderMeshPrimitives` / `Graphics.RenderMeshIndirect`
procedural-instanced draws.

Paired with `org.decentraland.instancing-assets`; both together raise `GPUI_PRO_PRESENT`,
which gates the tree-instancing call sites.

## Profile behaviour

Every `GPUIProfile` field the production assets author is resolved into a
`ProfileSnapshot` at registration and on each `SetParameterBufferData()`:

- **Distance and frustum culling** — `minMaxDistance.y`, `minCullingDistance`,
  `isDistanceCulling`, `isFrustumCulling`, `frustumOffset`, `boundsOffset`.
- **LOD selection** — Unity's LODGroup screen-relative-height model for
  perspective and orthographic cameras, scaled by `lodBiasAdjustment` and
  `QualitySettings.lodBias`, floored at `maximumLODLevel`.
- **LOD cross-fade** — `isLODCrossFade` dissolves LOD changes instead of
  popping. `isAnimateCrossFade = 0` blends over the last
  `lodCrossFadeTransitionWidth` of each LOD's range (Unity's spatial model);
  `isAnimateCrossFade = 1` dissolves over `1 / lodCrossFadeAnimateSpeed`
  seconds after the pick changes. Cross-fading instances draw twice, with a
  `LOD_FADE_CROSSFADE` clone of the material and a per-instance factor
  (`gpuiLODFadeBuffer`) that `setupGPUI()` writes into `unity_LODFade`;
  steady instances keep the plain material.
- **Shadow casters** — `isShadowCasting`, `isShadowDistanceCulling` (against
  `customShadowDistance`, else the URP asset's shadow distance),
  `minShadowCullingDistance`, `isShadowFrustumCulling`, `shadowFrustumOffset`
  and `shadowLODMap`. Casters beyond the shadow distance draw with
  `ShadowCastingMode.Off`; casters outside the view frustum that still reach
  it draw as `ShadowsOnly`.
- **Occlusion culling** — `isOcclusionCulling` runs a Hi-Z test on the GPU:
  a render pass enqueued on the driving camera rebuilds a depth pyramid after
  the opaques of every frame, and a compute pass tests each instance's bounds
  against the pyramid of the preceding frame, compacting the survivors for
  `RenderMeshIndirect`. `occlusionAccuracy` picks the pyramid level (2ⁿ
  texels of footprint), `occlusionOffset` / `occlusionOffsetSizeMultiplier`
  bias the test toward keeping. `isShadowOcclusionCulling = 0` demotes
  occluded casters to shadow-only draws so their shadows survive. Without a
  compute-capable device, or before the first driven frame has rendered, the
  draws go out directly.

`enablePerObjectMotionVectors` and `isDefaultProfile` only round-trip.
