using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Interaction.Raycast.Components;
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
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class InteractionHighlightSystem : BaseUnityLoopSystem
    {
        private readonly Material hoverMaterial;
        private readonly Material hoverOorMaterial;

        internal InteractionHighlightSystem(World world,
            Material hoverMaterial,
            Material hoverOorMaterial) : base(world)
        {
            this.hoverMaterial = hoverMaterial;
            this.hoverOorMaterial = hoverOorMaterial;
        }

        protected override void Update(float t)
        {
            UpdateHighlightsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateHighlights(ref HighlightComponent highlightComponent)
        {
            Material materialToUse = highlightComponent.IsAtDistance() ? hoverMaterial : hoverOorMaterial;

            if (highlightComponent.CurrentEntityOrNull() != EntityReference.Null
                && World.Has<DeleteEntityIntention>(highlightComponent.CurrentEntityOrNull()))
                highlightComponent.Disable();

            if (highlightComponent.CanPassAnUpdate(materialToUse))
                return;

            if (highlightComponent.ReadyForMaterial())
            {
                highlightComponent.UpdateMaterialAndSwitchEntity(materialToUse);
                TryAddHoverMaterials(ref highlightComponent, highlightComponent.CurrentEntityOrNull());
            }
            else
            {
                if (highlightComponent.IsEmpty())
                    return;

                TryRemoveHoverMaterialFromComponentSiblings(ref highlightComponent, highlightComponent.CurrentEntityOrNull());
                highlightComponent.MoveNextAndRemoveMaterial();
            }
        }

        private void TryAddHoverMaterials(ref HighlightComponent highlightComponent, in EntityReference entity)
        {
            List<Renderer> renderers = ListPool<Renderer>.Get();
            AddRenderersFromEntity(entity, renderers);

            TransformComponent entityTransform = World!.Get<TransformComponent>(entity);
            GetRenderersFromChildrenRecursive(ref entityTransform, renderers);

            foreach (Renderer renderer in renderers)
            {
                if (highlightComponent.OriginalMaterials.ContainsKey(renderer))
                    continue;

                List<Material> materials = ListPool<Material>.Get();
                highlightComponent.OriginalMaterials.Add(renderer, renderer.sharedMaterials);

                renderer.GetMaterials(materials);
                materials.Add(highlightComponent.MaterialOnUse());
                renderer.SetMaterials(materials);

                ListPool<Material>.Release(materials);
            }

            ListPool<Renderer>.Release(renderers);
        }

        private void TryRemoveHoverMaterialFromComponentSiblings(ref HighlightComponent highlightComponent, in EntityReference entity)
        {
            List<Renderer> renderers = ListPool<Renderer>.Get();
            AddRenderersFromEntity(entity, renderers);

            TransformComponent entityTransform = World!.Get<TransformComponent>(entity);
            GetRenderersFromChildrenRecursive(ref entityTransform, renderers);

            foreach (Renderer renderer in renderers)
            {
                if (!highlightComponent.OriginalMaterials.ContainsKey(renderer))
                    continue;

                renderer.sharedMaterials = highlightComponent.OriginalMaterials[renderer];
                highlightComponent.OriginalMaterials.Remove(renderer);
            }

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
