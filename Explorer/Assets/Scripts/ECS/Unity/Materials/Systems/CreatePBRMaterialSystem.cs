﻿using Arch.Core;
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
    public partial class CreatePBRMaterialSystem : CreateMaterialSystemBase
    {
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IPerformanceBudget capFrameBudget;

        public CreatePBRMaterialSystem(World world, IObjectPool<Material> materialsPool,
            IPerformanceBudget capFrameBudget, IPerformanceBudget memoryBudgetProvider) : base(world, materialsPool)
        {
            this.capFrameBudget = capFrameBudget;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        [All(typeof(ShouldInstanceMaterialComponent))]
        private void Handle(Entity entity, ref MaterialComponent materialComponent)
        {
            if (!materialComponent.Data.IsPbrMaterial)
                return;

            if (materialComponent.Status is not StreamableLoading.LifeCycle.LoadingInProgress)
                return;

            if (!capFrameBudget.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget())
                return;

            ConstructMaterial(entity, ref materialComponent);
        }

        private void ConstructMaterial(Entity entity, ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(ref materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2DData> albedoResult)
                && TryGetTextureResult(ref materialComponent.EmissiveTexPromise, out StreamableLoadingResult<Texture2DData> emissiveResult)
                && TryGetTextureResult(ref materialComponent.BumpTexPromise, out StreamableLoadingResult<Texture2DData> bumpResult))
            {
                materialComponent.Status = StreamableLoading.LifeCycle.LoadingFinished;

                materialComponent.Result ??= CreateNewMaterialInstance();

                SetUpColors(materialComponent.Result, materialComponent.Data.AlbedoColor, materialComponent.Data.EmissiveColor, materialComponent.Data.ReflectivityColor, materialComponent.Data.EmissiveIntensity);
                SetUpProps(materialComponent.Result, materialComponent.Data.Metallic, materialComponent.Data.Roughness, materialComponent.Data.SpecularIntensity, materialComponent.Data.DirectIntensity);
                SetUpTransparency(materialComponent.Result, materialComponent.Data.TransparencyMode, materialComponent.Data.AlbedoColor, materialComponent.Data.AlphaTest);

                TrySetTexture(materialComponent.Result, ref albedoResult, ShaderUtils.BaseMap, in materialComponent.Data.Textures.AlbedoTexture);
                TrySetTexture(materialComponent.Result, ref emissiveResult, ShaderUtils.EmissionMap, in materialComponent.Data.Textures.EmissiveTexture);
                TrySetTexture(materialComponent.Result, ref bumpResult, ShaderUtils.BumpMap, in materialComponent.Data.Textures.BumpTexture);

                DestroyEntityReferencesForPromises(ref materialComponent);

                World!.Remove<ShouldInstanceMaterialComponent>(entity);

                // TODO It is super expensive and allocates 500 KB every call, the changes must be made in the common library
                // SRPBatchingHelper.OptimizeMaterial(materialComponent.Result);
            }
        }

        public static void SetUpColors(Material material, Color albedo, Color emissive, Color reflectivity, float emissiveIntensity)
        {
            material.SetColor(ShaderUtils.BaseColor, albedo);

            if (emissive != Color.clear && emissive != Color.black) material.EnableKeyword("_EMISSION");

            material.SetColor(ShaderUtils.EmissionColor, emissive * emissiveIntensity);
            material.SetColor(ShaderUtils.SpecColor, reflectivity);
        }

        public static void SetUpProps(Material material, float metallic, float roughness,
            float specularIntensity, float directIntensity)
        {
            material.SetFloat(ShaderUtils.Metallic, metallic);
            material.SetFloat(ShaderUtils.Smoothness, 1 - roughness);
            material.SetFloat(ShaderUtils.SpecularHighlights, specularIntensity * directIntensity);
        }

        public static void SetUpTransparency(Material material, MaterialTransparencyMode transparencyMode, Color albedoColor, float alphaTest)
        {
            transparencyMode.ResolveAutoMode(null, albedoColor);

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
    }
}
