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
    public partial class ApplyMaterialSystem : BaseUnityLoopSystem
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
            switch (materialComponent.Status)
            {
                // If Material is loaded but not applied
                case MaterialComponent.LifeCycle.LoadingFinished:
                // If Material was applied once but renderer is dirty
                case MaterialComponent.LifeCycle.MaterialApplied when pbMeshRenderer.IsDirty:
                    materialComponent.Status = MaterialComponent.LifeCycle.MaterialApplied;

                    meshRendererComponent.MeshRenderer.sharedMaterial = materialComponent.Result;
                    meshRendererComponent.MeshRenderer.shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    break;
            }
        }
    }
}
