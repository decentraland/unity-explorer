using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Shaders;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ECS.Unity.Materials.Systems
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(StartMaterialsLoadingSystem))]
    public partial class CreateBasicMaterialSystem : CreateMaterialSystemBase
    {
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IPerformanceBudget capFrameBudget;

        public CreateBasicMaterialSystem(World world, IObjectPool<Material> materialsPool, IPerformanceBudget capFrameBudget, IPerformanceBudget memoryBudgetProvider) : base(world, materialsPool)
        {
            this.capFrameBudget = capFrameBudget;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        private void Handle(ref MaterialComponent materialComponent)
        {
            if (materialComponent.Data.IsPbrMaterial)
                return;

            if (!capFrameBudget.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget())
                return;

            if (materialComponent.Status == StreamableLoading.LifeCycle.LoadingInProgress)
                ConstructMaterial(ref materialComponent);
        }

        private void ConstructMaterial(ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(ref materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2DData> albedoResult) &&
                TryGetTextureResult(ref materialComponent.AlphaTexPromise, out StreamableLoadingResult<Texture2DData> alphaResult))
            {
                materialComponent.Status = StreamableLoading.LifeCycle.LoadingFinished;

                materialComponent.Result ??= CreateNewMaterialInstance();

                SetUp(materialComponent.Result, materialComponent.Data.AlphaTest, materialComponent.Data.DiffuseColor);

                SetUpTransparency(materialComponent.Result, materialComponent.Data.TransparencyMode, in materialComponent.Data.Textures.AlphaTexture, materialComponent.Data.AlbedoColor, materialComponent.Data.AlphaTest);

                TrySetTexture(materialComponent.Result, ref albedoResult, ShaderUtils.BaseMap, in materialComponent.Data.Textures.AlbedoTexture);
                TrySetTexture(materialComponent.Result, ref alphaResult, ShaderUtils.AlphaTexture, in materialComponent.Data.Textures.AlphaTexture);

                DestroyEntityReference(ref materialComponent.AlbedoTexPromise);
            }
        }

        public static void SetUpTransparency(Material material, MaterialTransparencyMode transparencyMode,
            in TextureComponent? alphaTexture, Color albedoColor, float alphaTest)
        {
            transparencyMode.ResolveAutoMode(alphaTexture, albedoColor);

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (transparencyMode)
            {
                case MaterialTransparencyMode.Opaque:
                    material.DisableKeyword("_ALPHATEST_ON"); // Cut Out Transparency
                    material.DisableKeyword("_ALPHABLEND_ON"); // Fade Transparency
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // Transparent

                    material.renderQueue = (int)RenderQueue.Geometry;
                    material.SetFloat(ShaderUtils.AlphaClip, 0);
                    break;
                case MaterialTransparencyMode.AlphaTest: // ALPHATEST
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON"); // Fade Transparency
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // Transparent

                    material.SetInt(ShaderUtils.SrcBlend, (int)BlendMode.One);
                    material.SetInt(ShaderUtils.DstBlend, (int)BlendMode.Zero);
                    material.SetInt(ShaderUtils.ZWrite, 1);
                    material.SetFloat(ShaderUtils.AlphaClip, 1);
                    material.SetFloat(ShaderUtils.Cutoff, alphaTest);
                    material.SetInt(ShaderUtils.Surface, 0);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case MaterialTransparencyMode.AlphaBlend: // ALPHABLEND
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // Transparent
                    material.EnableKeyword("_ALPHABLEND_ON");

                    material.SetInt(ShaderUtils.SrcBlend, (int)BlendMode.SrcAlpha);
                    material.SetInt(ShaderUtils.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt(ShaderUtils.ZWrite, 0);
                    material.SetFloat(ShaderUtils.AlphaClip, 0);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetInt(ShaderUtils.Surface, 1);
                    break;
                case MaterialTransparencyMode.AlphaTestAndAlphaBlend:
                    material.DisableKeyword("_ALPHATEST_ON"); // Cut Out Transparency
                    material.DisableKeyword("_ALPHABLEND_ON"); // Fade Transparency
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); // Transparent

                    material.SetInt(ShaderUtils.SrcBlend, (int)BlendMode.One);
                    material.SetInt(ShaderUtils.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt(ShaderUtils.ZWrite, 0);
                    material.SetFloat(ShaderUtils.AlphaClip, 1);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetInt(ShaderUtils.Surface, 1);
                    break;
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
