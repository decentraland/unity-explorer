using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Helpers;
using DCL.Shaders;
using ECS.StreamableLoading.Components.Common;
using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.Materials.Systems
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    public partial class CreatePBRMaterialSystem : CreateMaterialSystemBase
    {
        /// <summary>
        ///     The path from the shared package
        /// </summary>
        private const string MATERIAL_PATH = "Materials/ShapeMaterial";

        internal CreatePBRMaterialSystem(World world, IMaterialsCache materialsCache, int attemptsCount)
            : base(world, materialsCache, attemptsCount) { }

        protected override void Update(float t)
        {
            HandleQuery(World);
        }

        [Query]
        private void Handle(ref MaterialComponent materialComponent)
        {
            if (!materialComponent.Data.IsPbrMaterial)
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
            TryCreateGetTextureIntention(in materialComponent.Data.EmissiveTexture, ref materialComponent.EmissiveTexPromise);
            TryCreateGetTextureIntention(in materialComponent.Data.AlphaTexture, ref materialComponent.AlphaTexPromise);
            TryCreateGetTextureIntention(in materialComponent.Data.BumpTexture, ref materialComponent.BumpTexPromise);

            materialComponent.Status = MaterialComponent.LifeCycle.LoadingInProgress;
        }

        private void ConstructMaterial(ref MaterialComponent materialComponent)
        {
            // Check if all promises are finished
            // Promises are finished if: all of their entities are invalid, no promises at all, or the result component exists

            if (TryGetTextureResult(in materialComponent.AlbedoTexPromise, out StreamableLoadingResult<Texture2D> albedoResult)
                && TryGetTextureResult(in materialComponent.EmissiveTexPromise, out StreamableLoadingResult<Texture2D> emissiveResult)
                && TryGetTextureResult(in materialComponent.AlphaTexPromise, out StreamableLoadingResult<Texture2D> alphaResult)
                && TryGetTextureResult(in materialComponent.BumpTexPromise, out StreamableLoadingResult<Texture2D> bumpResult))
            {
                materialComponent.Status = MaterialComponent.LifeCycle.LoadingFinished;

                Material mat = CreateNewMaterialInstance();

                SetUpColors(mat, materialComponent.Data.AlbedoColor, materialComponent.Data.EmissiveColor, materialComponent.Data.ReflectivityColor, materialComponent.Data.EmissiveIntensity);
                SetUpProps(mat, materialComponent.Data.Metallic, materialComponent.Data.Roughness, materialComponent.Data.Glossiness, materialComponent.Data.SpecularIntensity, materialComponent.Data.DirectIntensity);
                SetUpTransparency(mat, materialComponent.Data.TransparencyMode, materialComponent.Data.AlphaTexture, materialComponent.Data.AlbedoColor, materialComponent.Data.AlphaTest);

                TrySetTexture(mat, ref albedoResult, ShaderUtils.BaseMap);
                TrySetTexture(mat, ref emissiveResult, ShaderUtils.EmissionMap);
                TrySetTexture(mat, ref alphaResult, ShaderUtils.AlphaTexture);
                TrySetTexture(mat, ref bumpResult, ShaderUtils.BumpMap);

                DestroyEntityReference(in materialComponent.AlbedoTexPromise);
                DestroyEntityReference(in materialComponent.EmissiveTexPromise);
                DestroyEntityReference(in materialComponent.AlphaTexPromise);
                DestroyEntityReference(in materialComponent.BumpTexPromise);

                SRPBatchingHelper.OptimizeMaterial(mat);

                materialComponent.Result = mat;

                materialsCache.Add(in materialComponent.Data, mat);
            }
        }

        protected override string materialPath => MATERIAL_PATH;

        public static void SetUpColors(Material material, Color albedo, Color emissive, Color reflectivity, float emissiveIntensity)
        {
            material.SetColor(ShaderUtils.BaseColor, albedo);

            if (emissive != Color.clear && emissive != Color.black) { material.EnableKeyword("_EMISSION"); }

            material.SetColor(ShaderUtils.EmissionColor, emissive * emissiveIntensity);
            material.SetColor(ShaderUtils.SpecColor, reflectivity);
        }

        public static void SetUpProps(Material material, float metallic, float roughness, float glossiness,
            float specularIntensity, float directIntensity)
        {
            material.SetFloat(ShaderUtils.Metallic, metallic);
            material.SetFloat(ShaderUtils.Smoothness, 1 - roughness);
            material.SetFloat(ShaderUtils.EnvironmentReflections, glossiness);
            material.SetFloat(ShaderUtils.SpecularHighlights, specularIntensity * directIntensity);
        }

        public static void SetUpTransparency(Material material, MaterialTransparencyMode transparencyMode,
            TextureComponent? alphaTexture, Color albedoColor, float alphaTest)
        {
            // Reset shader keywords
            material.DisableKeyword("_ALPHATEST_ON"); // Cut Out Transparency
            material.DisableKeyword("_ALPHABLEND_ON"); // Fade Transparency
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // Transparent

            if (transparencyMode == MaterialTransparencyMode.Auto)
            {
                if (alphaTexture != null || albedoColor.a < 1f) //AlphaBlend
                {
                    transparencyMode = MaterialTransparencyMode.AlphaBlend;
                }
                else // Opaque
                {
                    transparencyMode = MaterialTransparencyMode.Opaque;
                }
            }

            switch (transparencyMode)
            {
                case MaterialTransparencyMode.Opaque:
                    material.renderQueue = (int)RenderQueue.Geometry;
                    material.SetFloat(ShaderUtils.AlphaClip, 0);
                    break;
                case MaterialTransparencyMode.AlphaTest: // ALPHATEST
                    material.EnableKeyword("_ALPHATEST_ON");

                    material.SetInt(ShaderUtils.SrcBlend, (int)BlendMode.One);
                    material.SetInt(ShaderUtils.DstBlend, (int)BlendMode.Zero);
                    material.SetInt(ShaderUtils.ZWrite, 1);
                    material.SetFloat(ShaderUtils.AlphaClip, 1);
                    material.SetFloat(ShaderUtils.Cutoff, alphaTest);
                    material.SetInt(ShaderUtils.Surface, 0);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case MaterialTransparencyMode.AlphaBlend: // ALPHABLEND
                    material.EnableKeyword("_ALPHABLEND_ON");

                    material.SetInt(ShaderUtils.SrcBlend, (int)BlendMode.SrcAlpha);
                    material.SetInt(ShaderUtils.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt(ShaderUtils.ZWrite, 0);
                    material.SetFloat(ShaderUtils.AlphaClip, 0);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetInt(ShaderUtils.Surface, 1);
                    break;
                case MaterialTransparencyMode.AlphaTestAndAlphaBlend:
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");

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
