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
using SceneRunner.Scene;
using System.Collections.Generic;
using ECS.LifeCycle;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Interaction.Systems
{
    /// <summary>
    ///     Uses the HighlightComponent singleton component to highlight game objects
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class InteractionHighlightSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly InteractionSettingsData settingsData;
        private readonly ISceneStateProvider sceneStateProvider;

        internal InteractionHighlightSystem(World world, InteractionSettingsData settingsData, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.settingsData = settingsData;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            UpdateHighlightsQuery(World);
        }

        [Query]
        private void ResetHighlightComponent(ref HighlightComponent highlightComponent)
        {
            if (highlightComponent.IsEmpty())
                return;

            RemoveHighlight(highlightComponent.CurrentEntityOrNull());
            highlightComponent.MoveNextAndRemoveMaterial();
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
                if (highlightComponent.HasToResetLastEntity())
                    RemoveHighlight(highlightComponent.CurrentEntityOrNull());

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

            ref TransformComponent entityTransform = ref World.TryGetRef<TransformComponent>(entity, out bool containsTransform);

            // Fixes a crash by trying to access the transform of an entity when is not available
            if (!containsTransform)
            {
                ListPool<Renderer>.Release(renderers);
                return;
            }

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

            ref TransformComponent entityTransform = ref World.TryGetRef<TransformComponent>(entity, out bool containsTransform);

            // Fixes a crash by trying to access the transform of an entity when is not available
            if (!containsTransform)
            {
                ListPool<Renderer>.Release(renderers);
                return;
            }

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

                ref TransformComponent childTransform = ref World.TryGetRef<TransformComponent>(child, out bool containsTransform);
                if (!containsTransform) continue;

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

        public void OnSceneIsCurrentChanged(bool value)
        {
            if (!value)
            {
                ResetHighlightComponentQuery(World);
            }
        }
    }
}
