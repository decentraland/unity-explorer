using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Components;
using System.Collections.Generic;
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
                {
                    bool wasEnabled = renderers[i].enabled;
                    renderers[i].enabled = visible;
                    Vector3 rendererWorldPos = renderers[i].transform.position;
                    UnityEngine.Debug.Log($"[Visibility] GltfContainerVisibilitySystem: Renderer[{i}] " +
                        $"enabled: {wasEnabled} -> {visible}, " +
                        $"worldPos={rendererWorldPos}, " +
                        $"gameObject={renderers[i].gameObject.name}, " +
                        $"activeInHierarchy={renderers[i].gameObject.activeInHierarchy}, " +
                        $"activeSelf={renderers[i].gameObject.activeSelf}, " +
                        $"material={(renderers[i].sharedMaterial != null ? renderers[i].sharedMaterial.name : "NULL")}, " +
                        $"shader={(renderers[i].sharedMaterial != null ? renderers[i].sharedMaterial.shader.name : "NULL")}, " +
                        $"bounds={renderers[i].bounds}, " +
                        $"isVisible={renderers[i].isVisible}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[Visibility] GltfContainerVisibilitySystem: Renderer[{i}] is null!");
                }
            }
        }
    }
}
