using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
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
        private readonly EntityReference sceneRoot;

        public FinalizeGltfContainerLoadingSystem(World world, EntityReference sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
        }

        protected override void Update(float t)
        {
            FinalizeLoadingQuery(World);
            FinalizeLoadingNoTransformQuery(World);
        }

        /// <summary>
        ///     The overload that uses the scene transform as a parent
        /// </summary>
        /// <param name="component"></param>
        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(TransformComponent))]
        private void FinalizeLoadingNoTransform(ref GltfContainerComponent component)
        {
            if (World.TryGet(sceneRoot, out TransformComponent transformComponent))
                FinalizeLoading(ref component, ref transformComponent);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeLoading(ref GltfContainerComponent component, ref TransformComponent transformComponent)
        {
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

                // Reparent to the current transform
                result.Asset.Root.transform.SetParent(transformComponent.Transform);
                result.Asset.Root.transform.ResetLocalTRS();
                result.Asset.Root.SetActive(true);

                component.State.Set(LoadingState.Finished);
            }
        }
    }
}
