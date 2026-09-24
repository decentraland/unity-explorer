using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    /// <summary>
    ///     Draws primitives that share a mesh and an applied material through GPU instancing. Their
    ///     <see cref="MeshRenderer" /> stays alive for visibility, highlighting and bounds queries but is hidden
    ///     from rendering for as long as the entity is drawn instanced.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class RenderInstancedPrimitivesSystem : BaseUnityLoopSystem
    {
        private readonly PrimitiveInstanceBatches batches;

        internal RenderInstancedPrimitivesSystem(World world, PrimitiveInstanceBatches batches) : base(world)
        {
            this.batches = batches;
        }

        protected override void Update(float t)
        {
            batches.Clear();
            CollectInstancesQuery(World);
            RestoreRenderersWithoutMaterialQuery(World);
            batches.Render();
        }

        [Query]
        [All(typeof(PBMeshRenderer))]
        [None(typeof(DeleteEntityIntention))]
        private void CollectInstances(ref PrimitiveMeshRendererComponent rendererComponent, in MaterialComponent materialComponent, in TransformComponent transformComponent)
        {
            Material? material = materialComponent.Result;

            if (materialComponent.Status != LifeCycle.Applied || material == null || rendererComponent.PrimitiveMesh == null)
            {
                RestoreRenderer(ref rendererComponent);
                return;
            }

            if (!rendererComponent.InstancedRendering)
            {
                rendererComponent.MeshRenderer.forceRenderingOff = true;
                rendererComponent.InstancedRendering = true;
            }

            // Visibility systems toggle the renderer; an invisible primitive is not drawn instanced either
            if (!rendererComponent.MeshRenderer.enabled)
                return;

            ShadowCastingMode shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            batches.Add(rendererComponent.PrimitiveMesh.Mesh, material, shadowCastingMode, transformComponent.Transform.localToWorldMatrix);
        }

        [Query]
        [None(typeof(MaterialComponent), typeof(DeleteEntityIntention))]
        private void RestoreRenderersWithoutMaterial(ref PrimitiveMeshRendererComponent rendererComponent)
        {
            RestoreRenderer(ref rendererComponent);
        }

        private static void RestoreRenderer(ref PrimitiveMeshRendererComponent rendererComponent)
        {
            if (!rendererComponent.InstancedRendering)
                return;

            rendererComponent.MeshRenderer.forceRenderingOff = false;
            rendererComponent.InstancedRendering = false;
        }
    }
}
