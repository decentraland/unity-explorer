using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using Drakkar.GameUtils;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.SceneBoundsChecker;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Visibility.Systems;
using GPUInstancerPro;
using SceneRunner.Scene;
using System.Collections.Generic;
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
        private static int countee = 0;

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

            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                if (!result.Succeeded)
                {
                    component.State = LoadingState.FinishedWithError;
                    component.RootGameObject = null;
                    eventsBuffer.Add(entity, component);
                    result.TryLogException(GetReportData());
                    return;
                }

                ConfigureGltfContainerColliders.SetupColliders(ref component, result.Asset!);
                ConfigureSceneMaterial.EnableSceneBoundsAndForceCulling(in result.Asset!, in sceneCircumscribedPlanes, sceneHeight);

                entityCollidersSceneCache.Associate(in component, entity, sdkEntity);

                // Store reference to the root GameObject
                component.RootGameObject = result.Asset!.Root;

                // Re-parent to the current transform
                result.Asset!.Root.transform.SetParent(transformComponent.Transform);
                result.Asset.Root.transform.ResetLocalTRS();

                GameObject dvm = GameObject.Find("DrakkarVisibilityManager");
                if (!dvm.TryGetComponent<VisibilityManager>(out VisibilityManager visMan))
                {
                    visMan = dvm.AddComponent<VisibilityManager>();
                }

                foreach (var renderer in result.Asset.Renderers)
                {
                    GenerateAxisAlignedSpheres(renderer);
                }

                string strOccluder = "grid";

                foreach (var renderer in result.Asset.Renderers)
                {
                    if (renderer.gameObject.name.StartsWith(strOccluder))
                    {
                        var occ = renderer.gameObject.AddComponent<DrakkarOccluder>();

                        occ.Visibility = new VisibilityGroup();
                        VisibilitySphere[] viSpheres = renderer.gameObject.GetComponents<VisibilitySphere>();
                        occ.Visibility.CullingGroup = (int)((countee += viSpheres.Length) / 1000);
                        occ.Visibility.Visibility_Spheres = new VisibilitySphere[viSpheres.Length];

                        for (int i = 0; i < viSpheres.Length; ++i) { occ.Visibility.Visibility_Spheres[i] = viSpheres[i]; }

                        // frustumCheck defaults to NONE, which causes PrepareOccludersJob to skip every frame
                        occ.frustumCheck = FRUSTUM_CHECK.FULL;
                        // Min/Max define the local-space box in Drakkar's inverted convention (Min >= Max)
                        Bounds lb = renderer.localBounds;
                        occ.Min = lb.max;
                        occ.Max = lb.min;
                        // renderers must be set before Init() so renderersNum is populated for setVisibility()
                        occ.renderers = new[] { renderer };

                        occ.Init();
                        occ.Recalculate();
                        occ.tryRegister();
                    }
                }

                foreach (var renderer in result.Asset.Renderers)
                {
                    if (!renderer.gameObject.name.StartsWith(strOccluder) && !renderer.gameObject.TryGetComponent<DrakkarOccluder>(out DrakkarOccluder occ))
                    {
                        if (!renderer.gameObject.TryGetComponent<DrakkarOccludee>(out DrakkarOccludee occludee))
                        {
                            occludee = renderer.gameObject.AddComponent<DrakkarOccludee>();
                            occludee.Visibility = new VisibilityGroup();
                            VisibilitySphere[] viSpheres = renderer.gameObject.GetComponents<VisibilitySphere>();
                            occludee.Visibility.CullingGroup = (int)((countee += viSpheres.Length) / 1000);
                            occludee.Visibility.Visibility_Spheres = new VisibilitySphere[viSpheres.Length];

                            for (int i = 0; i < viSpheres.Length; ++i) { occludee.Visibility.Visibility_Spheres[i] = viSpheres[i]; }

                            // Min/Max define the local-space box in Drakkar's inverted convention (Min >= Max)
                            Bounds lb = renderer.localBounds;
                            occludee.Min = lb.max;
                            occludee.Max = lb.min;
                            // renderers must be set before Init() so renderersNum is populated for setVisibility()
                            occludee.renderers = new[] { renderer };

                            occludee.Init();
                            occludee.Recalculate();
                            occludee.tryRegister();
                        }
                    }
                }

                // foreach (var renderer in result.Asset.Renderers)
                // {
                //     if (renderer.gameObject.TryGetComponent<VisibilityDriver>(out VisibilityDriver visDri))
                //     {
                //         if (renderer.gameObject.TryGetComponent<VisibilityEvent>(out VisibilityEvent visEve))
                //             visEve.renderers.Add(renderer);
                //         continue;
                //     }
                //
                //     var vd = renderer.gameObject.AddComponent<VisibilityDriver>();
                //     vd.VisibilityGroup = new VisibilityGroup();
                //
                //     VisibilitySphere[] viSpheres = renderer.gameObject.GetComponents<VisibilitySphere>();
                //     vd.VisibilityGroup.CullingGroup = (int)((countee += viSpheres.Length) / 1000);
                //     vd.VisibilityGroup.Visibility_Spheres = new VisibilitySphere[viSpheres.Length];
                //
                //     for (int i = 0; i < viSpheres.Length; ++i)
                //     {
                //         vd.VisibilityGroup.Visibility_Spheres[i] = viSpheres[i];
                //     }
                //
                //     var ve = renderer.gameObject.AddComponent<VisibilityEvent>();
                //     ve.Driver = vd;
                //     ve.renderers.Add(renderer);
                // }

                result.Asset.Root.SetActive(true);

                result.Asset.ToggleAnimationState(true);

                component.State = LoadingState.Finished;
                eventsBuffer.Add(entity, component);

                if (result.Asset!.Animations.Count > 0 && result.Asset!.Animators.Count == 0)
                    World.Add(entity, new LegacyGltfAnimation());
            }
        }

        // Axis-aligned version with offset
        public static void GenerateAxisAlignedSpheres(Renderer rend)
        {
            Bounds original = rend.bounds;
            Vector3 size = original.size;
            Vector3 min = original.min;

            int[] dominanceArray =  new int[3] {0,1,2};
            int temp;

            if (size[dominanceArray[0]] < size[dominanceArray[1]])
            {
                temp = dominanceArray[0];
                dominanceArray[0] = dominanceArray[1];
                dominanceArray[1] = temp;
            }

            if (size[dominanceArray[1]] < size[dominanceArray[2]])
            {
                temp = dominanceArray[1];
                dominanceArray[1] = dominanceArray[2];
                dominanceArray[2] = temp;
            }

            if (size[dominanceArray[0]] < size[dominanceArray[1]])
            {
                temp = dominanceArray[0];
                dominanceArray[0] = dominanceArray[1];
                dominanceArray[1] = temp;
            }

            if (size[dominanceArray[0]] * 0.25f > size[dominanceArray[1]])
            {
                size[dominanceArray[1]] = size[dominanceArray[0]] * 0.25f;
            }

            if (size[dominanceArray[1]] * 0.5f > size[dominanceArray[2]])
            {
                size[dominanceArray[2]] = size[dominanceArray[1]] * 0.5f;
            }

            // Cap per-axis sphere counts to avoid exhausting the Culler's 1000-sphere limit across all renderers
            const int MAX_SPHERES_PER_AXIS = 2;
            int axisOneCount = Mathf.Min(MAX_SPHERES_PER_AXIS, Mathf.Max(1, Mathf.FloorToInt(size[dominanceArray[1]] / size[dominanceArray[2]])));
            int axisTwoCount = Mathf.Min(MAX_SPHERES_PER_AXIS, Mathf.Max(1, Mathf.FloorToInt(size[dominanceArray[0]] / size[dominanceArray[1]])));

            Vector3 subSize = Vector3.zero;
            subSize[dominanceArray[0]] = size[dominanceArray[0]] / axisTwoCount;
            subSize[dominanceArray[1]] = size[dominanceArray[1]] / axisOneCount;
            subSize[dominanceArray[2]] = size[dominanceArray[2]];

            Bounds sub = new Bounds();
            for (int i = 0; i < axisOneCount; i++)
            {
                for (int j = 0; j < axisTwoCount; j++)
                {
                    Vector3 subMin = min;
                    subMin[dominanceArray[1]] = min[dominanceArray[1]] + i * subSize[dominanceArray[1]];
                    subMin[dominanceArray[0]] = min[dominanceArray[0]] + j * subSize[dominanceArray[0]];

                    sub.SetMinMax(subMin, subMin + subSize);

                    var vs = rend.gameObject.AddComponent<VisibilitySphere>();
                    vs.Visibility = new CullVisibility() { Radius = sub.extents.magnitude, Offset = (sub.center - original.center) };
                    vs.Visibility.AutoCommanded = true;
                    vs.Visibility.Static = true;

                    if (rend.gameObject.TryGetComponent(out VisibilityBounds vb))
                    {
                        vb.visBounds.Add(sub);
                    }
                    else
                    {
                        var newVB = rend.gameObject.AddComponent<VisibilityBounds>();
                        newVB.visBounds.Add(sub);
                    }
                }
            }
        }
    }
}
