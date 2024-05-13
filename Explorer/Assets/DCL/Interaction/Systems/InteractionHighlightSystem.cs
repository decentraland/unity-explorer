using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Settings;
using DCL.Rendering.Highlight;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Interaction.Systems
{
    /// <summary>
    ///     Uses the HighlightComponent singleton component to highlight game objects
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class InteractionHighlightSystem : BaseUnityLoopSystem
    {
        private readonly InteractionSettingsData settingsData;

        internal InteractionHighlightSystem(World world, InteractionSettingsData settingsData) : base(world)
        {
            this.settingsData = settingsData;
        }

        protected override void Update(float t)
        {
            UpdateHighlightsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateHighlights(ref HighlightComponent highlightComponent)
        {

            if (highlightComponent.CurrentEntityOrNull() != EntityReference.Null
                && World.Has<DeleteEntityIntention>(highlightComponent.CurrentEntityOrNull()))
                highlightComponent.Disable();

            if (highlightComponent.ReadyForMaterial())
            {
                highlightComponent.SwitchEntity();
                AddOrUpdateHighlight(highlightComponent.CurrentEntityOrNull(), highlightComponent.IsAtDistance());
            }
            else
            {
                if (highlightComponent.IsEmpty())
                    return;

                RemoveHighlight(highlightComponent.CurrentEntityOrNull());
                highlightComponent.MoveNextAndRemoveMaterial();
            }
        }

        private void AddOrUpdateHighlight(in EntityReference entity, bool isAtDistance)
        {
            List<Renderer> renderers = ListPool<Renderer>.Get();
            AddRenderersFromEntity(entity, renderers);

            TransformComponent entityTransform = World!.Get<TransformComponent>(entity);
            GetRenderersFromChildrenRecursive(ref entityTransform, renderers);

            foreach (Renderer renderer in renderers)
            {
                if (!HighlightRendererFeature.m_HighLightRenderers.ContainsKey(renderer))
                {
                    HighlightRendererFeature.m_HighLightRenderers.Add(renderer, new HighlightSettings
                    {
                        Color = GetColor(isAtDistance),
                        Width = settingsData.Thickness,
                    });
                }
                else
                {
                    HighlightSettings highlightSettings = HighlightRendererFeature.m_HighLightRenderers[renderer];
                    highlightSettings.Color = GetColor(isAtDistance);
                    highlightSettings.Width = settingsData.Thickness;
                    HighlightRendererFeature.m_HighLightRenderers[renderer] = highlightSettings;
                }

            }

            ListPool<Renderer>.Release(renderers);
        }

        private Color GetColor(bool isAtDistance) =>
            isAtDistance ? settingsData.ValidColor : settingsData.InvalidColor;

        private void RemoveHighlight(in EntityReference entity)
        {
            List<Renderer> renderers = ListPool<Renderer>.Get();
            AddRenderersFromEntity(entity, renderers);

            TransformComponent entityTransform = World!.Get<TransformComponent>(entity);
            GetRenderersFromChildrenRecursive(ref entityTransform, renderers);

            foreach (Renderer renderer in renderers)
                if (HighlightRendererFeature.m_HighLightRenderers.ContainsKey(renderer))
                    HighlightRendererFeature.m_HighLightRenderers.Remove(renderer);

            ListPool<Renderer>.Release(renderers);
        }

        private void GetRenderersFromChildrenRecursive(ref TransformComponent entityTransform, List<Renderer> list)
        {
            foreach (EntityReference child in entityTransform.Children)
            {
                AddRenderersFromEntity(child, list);

                TransformComponent childTransform = World!.Get<TransformComponent>(child);
                GetRenderersFromChildrenRecursive(ref childTransform, list);
            }
        }

        private void AddRenderersFromEntity(EntityReference child, List<Renderer> renderers)
        {
            if (World.TryGet(child, out PrimitiveMeshRendererComponent primitiveMeshRendererComponent))
                renderers.Add(primitiveMeshRendererComponent.MeshRenderer);

            if (!World.TryGet(child, out GltfContainerComponent gltfContainer))
                return;

            if (!gltfContainer.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> loadingResult))
                return;

            if (loadingResult.Asset?.Renderers != null)
                renderers.AddRange(loadingResult.Asset.Renderers);
        }
    }
}
