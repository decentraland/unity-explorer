using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Helpers;
using DCL.Shaders;
using ECS.StreamableLoading.Components.Common;
using ECS.Unity.Materials.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.Materials.Systems
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    public partial class CreateBasicMaterialSystem : CreateMaterialSystemBase
    {
        private const string MATERIAL_PATH = "Materials/BasicShapeMaterial";

        internal CreateBasicMaterialSystem(World world, IMaterialsCache materialsCache, int attemptsCount) : base(world, materialsCache, attemptsCount) { }

        protected override string materialPath => MATERIAL_PATH;

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        private void Handle(ref MaterialComponent materialComponent)
        {
            if (materialComponent.Data.IsPbrMaterial)
                return;

            switch (materialComponent.Status)
            {
                case MaterialComponent.LifeCycle.LoadingNotStarted:
                    StartTexturesLoading(ref materialComponent);
                    break;
                case MaterialComponent.LifeCycle.LoadingInProgress:
                    ConstructMaterial(ref materialComponent);
                    break;
            }
        }

        private void StartTexturesLoading(ref MaterialComponent materialComponent)
        {
            TryCreateGetTextureIntention(in materialComponent.Data.AlbedoTexture, ref materialComponent.AlbedoTexPromise);
            materialComponent.Status = MaterialComponent.LifeCycle.LoadingInProgress;
        }

        private void ConstructMaterial(ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(in materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2D> albedoResult))
            {
                materialComponent.Status = MaterialComponent.LifeCycle.LoadingFinished;

                Material mat = CreateNewMaterialInstance();

                SetUp(mat, materialComponent.Data.AlphaTest, materialComponent.Data.DiffuseColor);

                TrySetTexture(mat, ref albedoResult, ShaderUtils.BaseMap);

                DestroyEntityReference(in materialComponent.AlbedoTexPromise);

                SRPBatchingHelper.OptimizeMaterial(mat);

                materialComponent.Result = mat;

                materialsCache.Add(in materialComponent.Data, mat);
            }
        }

        public static void SetUp(Material material, float alphaTest, Color diffuseColor)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetInt(ShaderUtils.ZWrite, 1);
            material.SetFloat(ShaderUtils.AlphaClip, 1);
            material.SetFloat(ShaderUtils.Cutoff, alphaTest);
            material.SetColor(ShaderUtils.BaseColor, diffuseColor);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
    }
}
