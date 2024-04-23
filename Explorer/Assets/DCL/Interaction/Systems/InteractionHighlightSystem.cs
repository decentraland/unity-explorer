using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Interaction.Raycast.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
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

            if (highlightComponent.CurrentEntity() != EntityReference.Null
                && World.Has<DeleteEntityIntention>(highlightComponent.CurrentEntity()))
                highlightComponent.Disable();

            if (highlightComponent.CanUpdate(materialToUse))
                return;

            if (highlightComponent.ReadyForMaterial())
            {
                highlightComponent.UpdateMaterial(materialToUse);

                TransformComponent transformComponent = World!.Get<TransformComponent>(highlightComponent.CurrentEntity());
                TryAddHoverMaterials(ref highlightComponent, in transformComponent);
            }
            else
            {
                if (highlightComponent.IsEmpty())
                    return;

                TransformComponent transformComponent = World!.Get<TransformComponent>(highlightComponent.CurrentEntity());
                TryRemoveHoverMaterialFromComponentSiblings(ref highlightComponent, in transformComponent);
                highlightComponent.MoveNextAndRemoveMaterial();
            }
        }

        private void TryAddHoverMaterials(ref HighlightComponent highlightComponent, in TransformComponent transformComponent)
        {
            if (!World.TryGet(transformComponent.Parent, out TransformComponent parentTransform)) return;

            foreach (EntityReference brother in parentTransform.Children)
            {
                // TODO: we should support other rendereables like gltf
                if (!World.TryGet(brother, out PrimitiveMeshRendererComponent primitiveMeshRendererComponent))
                    continue;

                if (highlightComponent.OriginalMaterials.ContainsKey(brother))
                    continue;

                List<Material> materials = ListPool<Material>.Get();
                MeshRenderer renderer = primitiveMeshRendererComponent.MeshRenderer;
                highlightComponent.OriginalMaterials.Add(brother, renderer.sharedMaterials);

                // override materials
                renderer.GetMaterials(materials);
                materials.Add(highlightComponent.MaterialOnUse());
                renderer.SetMaterials(materials);

                ListPool<Material>.Release(materials);
            }
        }

        private void TryRemoveHoverMaterialFromComponentSiblings(ref HighlightComponent highlightComponent, in TransformComponent transformComponent)
        {
            if (!World.TryGet(transformComponent.Parent, out TransformComponent parentTransform)) return;

            foreach (EntityReference brother in parentTransform.Children)
            {
                if (!World.TryGet(brother, out PrimitiveMeshRendererComponent primitiveMeshRendererComponent))
                    continue;

                if (!highlightComponent.OriginalMaterials.ContainsKey(brother)) continue;

                primitiveMeshRendererComponent.MeshRenderer.sharedMaterials = highlightComponent.OriginalMaterials[brother];
                highlightComponent.OriginalMaterials.Remove(brother);
            }
        }
    }
}
