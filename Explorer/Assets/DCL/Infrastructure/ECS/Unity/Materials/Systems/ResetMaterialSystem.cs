using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Resets material to the default one if the component was deleted
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateBefore(typeof(ApplyMaterialSystem))]
    [ThrottlingEnabled]
    public partial class ResetMaterialSystem : BaseUnityLoopSystem
    {
        private readonly DestroyMaterial destroyMaterial;
        private readonly ISceneData sceneData;

        public ResetMaterialSystem(World world, DestroyMaterial destroyMaterial, ISceneData sceneData) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            ResetPrimitiveMeshQuery(World);
            ResetGltfContainerQuery(World);
        }

        [Query]
        [None(typeof(PBMaterial))]
        private void ResetPrimitiveMesh(Entity entity, ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            meshRendererComponent.SetDefaultMaterial(sceneData.Geometry.CircumscribedPlanes, sceneData.Geometry.Height);
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
            World.Remove<MaterialComponent>(entity);
        }

        [Query]
        [None(typeof(PBMaterial))]
        private void ResetGltfContainer(Entity entity, ref GltfContainerComponent gltfContainerComponent, ref MaterialComponent materialComponent)
        {
            if(gltfContainerComponent.OriginalMaterials == null) return;

            foreach (var originalMaterial in gltfContainerComponent.OriginalMaterials)
            {
                originalMaterial.renderer.sharedMaterial = originalMaterial.material;
            }

            gltfContainerComponent.OriginalMaterials.Clear();
            gltfContainerComponent.OriginalMaterials = null;

            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
            World.Remove<MaterialComponent>(entity);
        }
    }
}
