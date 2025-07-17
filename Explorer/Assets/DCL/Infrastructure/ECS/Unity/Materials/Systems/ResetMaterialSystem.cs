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
using UnityEngine.Rendering;

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
        [None(typeof(PBMaterial), typeof(GltfNode))]
        private void ResetPrimitiveMesh(Entity entity, ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            meshRendererComponent.SetDefaultMaterial(sceneData.Geometry.CircumscribedPlanes, sceneData.Geometry.Height);
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
            World.Remove<MaterialComponent>(entity);
        }

        [Query]
        [None(typeof(PBMaterial), typeof(PrimitiveMeshRendererComponent))]
        private void ResetGltfNode(Entity entity, ref GltfNode gltfNode, ref MaterialComponent materialComponent)
        {
            var gltfContainer = World.TryGetRef<GltfContainerComponent>(gltfNode.ContainerEntity, out bool exists);
            if (!exists) return;

                        // Reset all renderers to their original state
            foreach (var renderer in gltfNode.Renderers)
            {
                if (gltfContainer.OriginalMaterials!.TryGetValue(renderer, out var originalMaterial))
                    renderer.sharedMaterial = originalMaterial;
            }

            // Clean up the material component and remove the entity
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
            World.Remove<MaterialComponent>(entity);
            gltfContainer.GltfNodeEntities?.Remove(entity);
            
            // Destroy the GltfNode entity (unless it's the container entity itself)
            if (entity != gltfNode.ContainerEntity)
                World.Destroy(entity);
        }
    }
}
