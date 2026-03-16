using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Components;
using System.Collections.Generic;
using Temp.Helper.WebClient;
using UnityEngine;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(GltfContainerGroup))]
    public partial class GltfContainerVisibilitySystem : VisibilitySystemBase<GltfContainerComponent>
    {
        internal GltfContainerVisibilitySystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world, eventsBuffer)
        {

        }

        protected override void UpdateVisibilityInternal(in GltfContainerComponent component, bool visible)
        {
            // we have several states that are notified with events
            if (component.State != LoadingState.Finished) return;

            List<Renderer> renderers = component.Promise.Result!.Value.Asset!.Renderers;

            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
                else
                {
                    WebGLDebugLog.LogWarning($"[Visibility] GltfContainerVisibilitySystem: Renderer[{i}] is null!");
                }
            }
        }
    }
}
