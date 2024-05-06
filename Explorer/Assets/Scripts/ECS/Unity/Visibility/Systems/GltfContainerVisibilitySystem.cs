using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(FinalizeGltfContainerLoadingSystem))]
    public partial class GltfContainerVisibilitySystem : BaseUnityLoopSystem
    {
        internal GltfContainerVisibilitySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World);
            HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateVisibility(ref PBVisibilityComponent visibilityComponent,
            ref PBGltfContainer sdkComponent, ref GltfContainerComponent component)
        {
            // If the component has changed and was already loaded or component was resolved to the finished state this frame
            if (((sdkComponent.IsDirty || visibilityComponent.IsDirty) && component.State.Value == LoadingState.Finished)
                || component.State.ChangedThisFrameTo(LoadingState.Finished))
            {
                if (!component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) || !result.Succeeded)
                    return;

                List<Renderer> renderers = result.Asset!.Renderers;

                for (var i = 0; i < renderers.Count; i++)
                    renderers[i].enabled = visibilityComponent.GetVisible();
            }
        }

        [Query]
        [None(typeof(PBVisibilityComponent))]
        private void HandleComponentRemoval(ref RemovedComponents removedComponents, ref GltfContainerComponent component)
        {
            if (removedComponents.Set.Remove(typeof(PBVisibilityComponent)) && component.State == LoadingState.Finished)
            {
                List<Renderer> renderers = component.Promise.Result.Value.Asset.Renderers;

                for (var i = 0; i < renderers.Count; i++)
                    renderers[i].enabled = true;
            }
        }
    }
}
