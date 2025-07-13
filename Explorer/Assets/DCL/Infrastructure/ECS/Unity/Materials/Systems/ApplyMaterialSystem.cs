using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
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
            ApplyMaterialToPrimitiveMeshQuery(World);
            ApplyMaterialToGltfQuery(World);
        }

        [Query]
        [All(typeof(PBMaterial))]
        private void ApplyMaterialToPrimitiveMesh(ref PBMeshRenderer pbMeshRenderer,
            ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            switch (materialComponent.Status)
            {
                // If Material is loaded but not applied
                case StreamableLoading.LifeCycle.LoadingFinished:
                // If Material was applied once but renderer is dirty
                case StreamableLoading.LifeCycle.Applied when pbMeshRenderer.IsDirty:
                    materialComponent.Status = StreamableLoading.LifeCycle.Applied;

                    ReleaseMaterial.TryReleaseDefault(ref meshRendererComponent);
                    ConfigureSceneMaterial.EnableSceneBounds(materialComponent.Result, sceneData.Geometry.CircumscribedPlanes, sceneData.Geometry.Height);

                    meshRendererComponent.MeshRenderer.sharedMaterial = materialComponent.Result;
                    meshRendererComponent.MeshRenderer.shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    break;
            }
        }

        [Query]
        [All(typeof(PBMaterial))]
        private void ApplyMaterialToGltf(ref GltfContainerComponent gltfContainer, ref MaterialComponent materialComponent, ref PBMaterial pbMaterial)
        {
            if (gltfContainer.State != LoadingState.Finished) return;

            switch (materialComponent.Status)
            {
                case StreamableLoading.LifeCycle.LoadingFinished:
                case StreamableLoading.LifeCycle.Applied when pbMaterial.IsDirty:

                    if (!gltfContainer.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) || !result.Succeeded)
                        return;

                    materialComponent.Status = StreamableLoading.LifeCycle.Applied;

                    GltfContainerAsset asset = result.Asset;

                    if (gltfContainer.OriginalMaterials == null)
                    {
                        gltfContainer.OriginalMaterials = new List<(Renderer renderer, Material material)>();
                        foreach (var renderer in asset.Renderers)
                        {
                            gltfContainer.OriginalMaterials.Add((renderer, renderer.sharedMaterial));
                        }
                    }

                    ConfigureSceneMaterial.EnableSceneBounds(materialComponent.Result, sceneData.Geometry.CircumscribedPlanes, sceneData.Geometry.Height);

                    for (var i = 0; i < asset.Renderers.Count; i++)
                    {
                        var renderer = asset.Renderers[i];
                        renderer.sharedMaterial = materialComponent.Result;
                        renderer.shadowCastingMode = materialComponent.Data.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    }
                    break;
            }
        }
    }
}
