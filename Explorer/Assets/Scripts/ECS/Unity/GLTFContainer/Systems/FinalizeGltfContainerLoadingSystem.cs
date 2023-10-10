using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Transforms.Components;
using Utility;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Resolves GltfContainerAsset promise
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    public partial class FinalizeGltfContainerLoadingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IConcurrentBudgetProvider capBudget;
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;

        public FinalizeGltfContainerLoadingSystem(World world, Entity sceneRoot, IConcurrentBudgetProvider capBudget,
            IEntityCollidersSceneCache entityCollidersSceneCache) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.capBudget = capBudget;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
        }

        protected override void Update(float t)
        {
            ref TransformComponent sceneTransform = ref World.Get<TransformComponent>(sceneRoot);

            FinalizeLoadingQuery(World);
            FinalizeLoadingNoTransformQuery(World, ref sceneTransform);
        }

        /// <summary>
        ///     The overload that uses the scene transform as a parent
        /// </summary>
        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(TransformComponent))]
        private void FinalizeLoadingNoTransform([Data] ref TransformComponent sceneTransform, in Entity entity, ref CRDTEntity sdkEntity, ref GltfContainerComponent component)
        {
            FinalizeLoading(in entity, ref sdkEntity, ref component, ref sceneTransform);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeLoading(in Entity entity, ref CRDTEntity sdkEntity, ref GltfContainerComponent component, ref TransformComponent transformComponent)
        {
            if (!capBudget.TrySpendBudget())
                return;

            // Try consume removes the entity if the loading is finished
            if (component.State.Value == LoadingState.Loading
                && component.Promise.TryConsume(World, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                if (!result.Succeeded)
                {
                    component.State.Set(LoadingState.FinishedWithError);
                    return;
                }

                ConfigureGltfContainerColliders.SetupColliders(ref component, result.Asset);

                entityCollidersSceneCache.Associate(in component, World.Reference(entity), sdkEntity);

                // Reparent to the current transform
                result.Asset.Root.transform.SetParent(transformComponent.Transform);
                result.Asset.Root.transform.ResetLocalTRS();
                result.Asset.Root.SetActive(true);

                component.State.Set(LoadingState.Finished);
            }
        }
    }
}
