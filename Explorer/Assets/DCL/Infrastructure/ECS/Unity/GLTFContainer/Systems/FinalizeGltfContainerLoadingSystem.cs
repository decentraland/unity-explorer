using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
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

                // A stale result can still reference an asset whose Root was already destroyed (e.g. drained
                // by cache Unload/Remove). The promise is consumed at this point, so the component must still
                // reach a terminal state; leaving it Loading would re-enter this query and throw
                // "already consumed" on every subsequent frame.
                if (result.Asset is not { } asset || asset.Root == null)
                {
                    ReportHub.LogError(GetReportData(), $"GltfContainerAsset '{component.Name}' ({component.Hash}) resolved with a destroyed Root");
                    component.State = LoadingState.FinishedWithError;
                    component.RootGameObject = null;
                    eventsBuffer.Add(entity, component);
                    return;
                }

                ConfigureGltfContainerColliders.SetupColliders(ref component, asset);
                ConfigureSceneMaterial.EnableSceneBoundsAndForceCulling(in asset, in sceneCircumscribedPlanes, sceneHeight);

                entityCollidersSceneCache.Associate(in component, entity, sdkEntity);

                // Store reference to the root GameObject
                component.RootGameObject = asset.Root;

                // Re-parent to the current transform
                asset.Root.transform.SetParent(transformComponent.Transform);
                asset.Root.transform.ResetLocalTRS();
                asset.Root.SetActive(true);

                asset.SetRenderersActive(true);
                asset.ToggleAnimationState(true);

                component.State = LoadingState.Finished;
                eventsBuffer.Add(entity, component);

                if (asset.Animations.Count > 0 && asset.Animators.Count == 0)
                    World.Add(entity, new LegacyGltfAnimation());
            }
        }
    }
}
