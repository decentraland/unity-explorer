using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;
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
        public FinalizeGltfContainerLoadingSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            FinalizeLoadingQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeLoading(ref GltfContainerComponent component, ref TransformComponent transformComponent)
        {
            // Try consume removes the entity if the loading is finished
            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                // TODO error reporting
                if (!result.Succeeded)
                {
                    component.State.Set(LoadingState.FinishedWithError);
                    Debug.LogException(result.Exception);
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
