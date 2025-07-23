using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GltfNodeModifiers.Components;
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
            ResetGltfNodeQuery(World);
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
        private void ResetGltfNode(Entity entity, ref GltfNodeMaterialCleanupIntention cleanupIntention, ref MaterialComponent materialComponent)
        {
            if (!World.TryGet<ECS.Unity.GltfNodeModifiers.Components.GltfNodeModifiers>(cleanupIntention.ContainerEntity, out var gltfNodeModifiers)) return;

            // Reset all renderers to their original state
            foreach (var renderer in cleanupIntention.Renderers)
            {
                if (gltfNodeModifiers.OriginalMaterials.TryGetValue(renderer, out var originalMaterial))
                    renderer.sharedMaterial = originalMaterial;
            }

            // Clean up the material component and remove the entity
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
            World.Remove<PBMaterial, MaterialComponent, GltfNodeMaterialCleanupIntention>(entity);

            // Destroy the entity if requested and it's not the container entity itself
            if (cleanupIntention.Destroy && entity != cleanupIntention.ContainerEntity)
                World.Destroy(entity);
        }
    }
}
