using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using UnityEngine.Rendering;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Applies Material to the Renderer
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(CreateBasicMaterialSystem))]
    [UpdateAfter(typeof(CreatePBRMaterialSystem))]
    public class ApplyMaterialSystem : BaseUnityLoopSystem
    {
        internal ApplyMaterialSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            TryApplyMaterialQuery(World);
        }

        [Query]
        [All(typeof(PBMaterial))]
        private void TryApplyMaterial(ref PBMeshRenderer pbMeshRenderer,
            ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            if (

                // If Material is loaded but not applied
                materialComponent.Status == MaterialComponent.LifeCycle.LoadingFinished

                // or if MeshRenderer is dirty
                || pbMeshRenderer.IsDirty)
            {
                // apply it

                materialComponent.Status = MaterialComponent.LifeCycle.MaterialApplied;

                meshRendererComponent.MeshRenderer.sharedMaterial = materialComponent.Result;
                meshRendererComponent.MeshRenderer.shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }
    }
}
