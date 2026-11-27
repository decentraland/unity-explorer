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
    ///     Resets material to the default one if the component was deleted or updated to an empty one (neither Pbr nor Unlit set)
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
            ResetPrimitiveMeshWithEmptyMaterialQuery(World);
            ResetGltfNodeWithEmptyMaterialQuery(World);
        }

        [Query]
        private void ResetPrimitiveMeshWithEmptyMaterial(Entity entity, ref PBMaterial material, ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            if (material.MaterialCase == PBMaterial.MaterialOneofCase.None)
                ResetPrimitiveMesh(entity, ref meshRendererComponent, ref materialComponent);
        }

        [Query]
        private void ResetGltfNodeWithEmptyMaterial(Entity entity, ref PBMaterial material, ref GltfNode gltfNode, ref MaterialComponent materialComponent)
        {
            if (material.MaterialCase == PBMaterial.MaterialOneofCase.None)
                ResetGltfNode(entity, ref gltfNode, ref materialComponent);
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
        private void ResetGltfNode(Entity entity, ref GltfNode gltfNode, ref MaterialComponent materialComponent)
        {
            // Restore only while the container still tracks the original materials; the release and the node teardown below run regardless
            if (World.IsAlive(gltfNode.ContainerEntity)
                && World.TryGet(gltfNode.ContainerEntity, out ECS.Unity.GltfNodeModifiers.Components.GltfNodeModifiers gltfNodeModifiers))
            {
                // Reset all renderers to their original state
                foreach (var renderer in gltfNode.Renderers)
                {
                    if (gltfNodeModifiers.OriginalMaterials.TryGetValue(renderer, out var originalMaterial))
                        renderer.sharedMaterial = originalMaterial;
                }
            }

            // Clean up the material
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);

            // Destroy the entity if requested and it's not the container entity itself
            if (gltfNode.CleanupDestruction && entity != gltfNode.ContainerEntity)
                World.Destroy(entity);
            else
                World.Remove<MaterialComponent>(entity);
        }
    }
}
