using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;
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
        private readonly ISceneData sceneData;

        public ApplyMaterialSystem(World world, ISceneData sceneData) : base(world)
        {
            this.sceneData = sceneData;
        }

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
                case StreamableLoading.LifeCycle.LoadingFinished:
                // If Material was applied once but renderer is dirty
                case StreamableLoading.LifeCycle.Applied when pbMeshRenderer.IsDirty:
                    materialComponent.Status = StreamableLoading.LifeCycle.Applied;

                    meshRendererComponent.TryReleaseDefaultMaterial();
                    ConfigureSceneMaterial.EnableSceneBounds(materialComponent.Result, sceneData.Geometry.CircumscribedPlanes);

                    meshRendererComponent.MeshRenderer.sharedMaterial = materialComponent.Result;
                    meshRendererComponent.MeshRenderer.shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    break;
            }
        }
    }
}
