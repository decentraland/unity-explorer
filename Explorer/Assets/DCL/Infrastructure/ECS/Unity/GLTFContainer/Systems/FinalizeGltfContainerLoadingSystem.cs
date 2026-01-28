using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.SceneBoundsChecker;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Visibility.Systems;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Resolves GltfContainerAsset promise
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    [UpdateBefore(typeof(GltfContainerVisibilitySystem))]
    public partial class FinalizeGltfContainerLoadingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IPerformanceBudget capBudget;
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;
        private readonly ISceneData sceneData;
        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;

        public FinalizeGltfContainerLoadingSystem(World world, Entity sceneRoot, IPerformanceBudget capBudget,
            IEntityCollidersSceneCache entityCollidersSceneCache, ISceneData sceneData, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.capBudget = capBudget;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            this.sceneData = sceneData;
            this.eventsBuffer = eventsBuffer;
        }

        protected override void Update(float t)
        {
            ref TransformComponent sceneTransform = ref World!.Get<TransformComponent>(sceneRoot);
            ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes = sceneData.Geometry.CircumscribedPlanes;

            FinalizeLoadingQuery(World, in sceneCircumscribedPlanes, sceneData.Geometry.Height);
            FinalizeLoadingNoTransformQuery(World, ref sceneTransform, in sceneCircumscribedPlanes, sceneData.Geometry.Height);
        }

        /// <summary>
        ///     The overload that uses the scene transform as a parent
        /// </summary>
        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(TransformComponent))]
        private void FinalizeLoadingNoTransform([Data] ref TransformComponent sceneTransform, [Data] in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes,
            [Data] float sceneHeight, in Entity entity, ref CRDTEntity sdkEntity, ref GltfContainerComponent component)
        {
            FinalizeLoading(in sceneCircumscribedPlanes, sceneHeight, in entity, ref sdkEntity, ref component, ref sceneTransform);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeLoading([Data] in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, [Data] float sceneHeight,
            in Entity entity, ref CRDTEntity sdkEntity, ref GltfContainerComponent component, ref TransformComponent transformComponent)
        {
            if (!capBudget.TrySpendBudget())
                return;

            if (component.State == LoadingState.Loading)
            {
                // Check if promise is ready
                bool canConsume = component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result);

                if (!canConsume)
                {
                    // Log periodically to avoid spam
                    if (Time.frameCount % 60 == 0)
                    {
                        bool promiseExists = component.Promise.Entity != Entity.Null;
                        bool hasResult = promiseExists && World!.Has<StreamableLoadingResult<GltfContainerAsset>>(component.Promise.Entity);

                        Debug.Log($"[AB-Loading] FinalizeGltfContainerLoadingSystem: entity={entity.Id} still Loading, " +
                                  $"promiseEntity={component.Promise.Entity.Id}, " +
                                  $"promiseExists={promiseExists}, " +
                                  $"hasResult={hasResult}, " +
                                  $"hash={component.Hash}");
                    }

                    return;
                }

                // Can consume - proceed with finalization
                {
                    if (!result.Succeeded)
                    {
                        Debug.LogWarning($"[AB-Loading] FinalizeGltfContainerLoadingSystem: FinishedWithError entity={entity.Id}");
                        component.State = LoadingState.FinishedWithError;
                        component.RootGameObject = null;
                        eventsBuffer.Add(entity, component);
                        return;
                    }

                    bool rootNull = result.Asset!.Root == null;
                    int rendererCount = result.Asset!.Renderers?.Count ?? 0;
                    Debug.Log($"[AB-Loading] FinalizeGltfContainerLoadingSystem: finalizing entity={entity.Id}, Root==null={rootNull}, renderers={rendererCount}");

                    ConfigureGltfContainerColliders.SetupColliders(ref component, result.Asset!);
                    ConfigureSceneMaterial.EnableSceneBounds(in result.Asset!, in sceneCircumscribedPlanes, sceneHeight);

                    entityCollidersSceneCache.Associate(in component, entity, sdkEntity);

                    // Store reference to the root GameObject
                    component.RootGameObject = result.Asset!.Root;

                    // Re-parent to the current transform
                    result.Asset!.Root.transform.SetParent(transformComponent.Transform);
                    result.Asset.Root.transform.ResetLocalTRS();
                    result.Asset.Root.SetActive(true);

                    // Log position information
                    Vector3 rootWorldPos = result.Asset.Root.transform.position;
                    Vector3 transformPos = transformComponent.Transform.position;

                    Debug.Log($"[AB-Loading] Finalize: entity={entity.Id}, " +
                              $"Root worldPos={rootWorldPos}, " +
                              $"Transform worldPos={transformPos}, " +
                              $"Root localPos={result.Asset.Root.transform.localPosition}");

                    // Log detailed renderer state after activation
                    if (result.Asset.Renderers != null && result.Asset.Renderers.Count > 0)
                    {
                        for (int i = 0; i < result.Asset.Renderers.Count; i++)
                        {
                            var renderer = result.Asset.Renderers[i];

                            if (renderer != null)
                            {
                                Vector3 rendererWorldPos = renderer.transform.position;
                                Material mat = renderer.sharedMaterial;

                                // Log texture info
                                string textureInfo = "no material";
                                if (mat != null)
                                {
                                    var texNames = new System.Text.StringBuilder();
                                    var texProps = new[] { "_BaseMap", "_MainTex", "_BumpMap", "_EmissionMap", "_MetallicGlossMap" };
                                    foreach (var prop in texProps)
                                    {
                                        if (mat.HasProperty(prop))
                                        {
                                            Texture tex = mat.GetTexture(prop);
                                            texNames.Append($"{prop}={(tex != null ? tex.name : "NULL")}, ");
                                        }
                                    }
                                    textureInfo = texNames.Length > 0 ? texNames.ToString() : "no texture properties";
                                }

                                Debug.Log($"[AB-Loading] Finalize: Renderer[{i}] entity={entity.Id}, " +
                                          $"enabled={renderer.enabled}, " +
                                          $"worldPos={rendererWorldPos}, " +
                                          $"activeInHierarchy={renderer.gameObject.activeInHierarchy}, " +
                                          $"activeSelf={renderer.gameObject.activeSelf}, " +
                                          $"material={(mat != null ? mat.name : "NULL")}, " +
                                          $"shader={(mat != null ? mat.shader.name : "NULL")}, " +
                                          $"textures=[{textureInfo}], " +
                                          $"bounds={renderer.bounds}");
                            }
                        }
                    }
                    else { Debug.LogWarning($"[AB-Loading] FinalizeGltfContainerLoadingSystem: No renderers found for entity={entity.Id}"); }

                    result.Asset.ToggleAnimationState(true);

                    component.State = LoadingState.Finished;
                    eventsBuffer.Add(entity, component);

                    if (result.Asset!.Animations.Count > 0 && result.Asset!.Animators.Count == 0)
                        World.Add(entity, new LegacyGltfAnimation());
                }
            }
        }
    }
}
