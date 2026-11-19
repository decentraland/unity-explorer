using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public static partial class AvatarMaterialConfiguration
    {
        private delegate Color GetFacialFeatureColor(in AvatarShapeComponent shapeComponent);

        private static readonly int BASE_COLOR = ComputeShaderConstants.BASE_COLOUR_SHADER_ID;
        private static readonly int CLIPPING_LEVEL = Shader.PropertyToID("_Clipping_Level");
        private static readonly int CULL_MODE = Shader.PropertyToID("_CullMode");

        private static readonly int Z_WRITE_MODE = Shader.PropertyToID("_ZWriteMode");
        private static readonly int CULL = Shader.PropertyToID("_Cull");
        private static readonly int Z_WRITE = Shader.PropertyToID("_ZWrite");
        private static readonly int ALPHA_CLIP = Shader.PropertyToID("_AlphaClip");
        private static readonly int TWEAK_TRANSPARENCY = Shader.PropertyToID("_Tweak_transparency");
        private static readonly int CUTOFF = Shader.PropertyToID("_Cutoff");
        private static readonly int SURFACE = Shader.PropertyToID("_Surface");
        private static readonly int SRC_BLEND = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND = Shader.PropertyToID("_DstBlend");
        private static readonly int ALPHA_SRC_BLEND_TARGET = Shader.PropertyToID("_AlphaSrcBlend");
        private static readonly int ALPHA_SRC_BLEND_ORIGINAL = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int ALPHA_DST_BLEND_TARGET = Shader.PropertyToID("_AlphaDstBlend");
        private static readonly int ALPHA_DST_BLEND_ORIGINAL = Shader.PropertyToID("_DstBlendAlpha");

        private static readonly (string suffix, string category, int defaultSlotIndexUsed, GetFacialFeatureColor getColor)[] SUFFIX_CATEGORY_MAP =
        {
            ("Mask_Eyes", WearableCategories.Categories.EYES, defaultSlotIndexUsed: 1, (in AvatarShapeComponent shape) => shape.EyesColor),
            ("Mask_Eyebrows", WearableCategories.Categories.EYEBROWS, defaultSlotIndexUsed: 0, (in AvatarShapeComponent shape) => shape.HairColor),
            ("Mask_Mouth", WearableCategories.Categories.MOUTH, defaultSlotIndexUsed: 0, (in AvatarShapeComponent shape) => shape.SkinColor),
        };

        private const string MISSING_TOON_POOL_MESSAGE = "No avatar material pool is registered for the toon shader, the renderer is skipped";
        private const string MISSING_FACIAL_FEATURES_POOL_MESSAGE = "No avatar material pool is registered for the facial features shader, the renderer is skipped";

        public static bool TrySetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler poolHandler,
            AvatarShapeComponent avatarShapeComponent,
            in FacialFeaturesTextures facialFeatures,
            out AvatarCustomSkinningComponent.MaterialSetup materialSetup)
        {
            bool isFacialFeature = TryGetFacialFeature(meshRenderer, avatarShapeComponent, facialFeatures, out Dictionary<int, Texture>? facialTextures, out Color facialColor, out int defaultSlotIndexUsed);
            int shaderId = isFacialFeature ? TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES : TextureArrayConstants.SHADERID_DCL_TOON;

            if (!poolHandler.TryGetMaterialPool(shaderId, out PoolMaterialSetup poolMaterialSetup))
            {
                ReportHub.LogError(ReportCategory.AVATAR, isFacialFeature ? MISSING_FACIAL_FEATURES_POOL_MESSAGE : MISSING_TOON_POOL_MESSAGE);
                materialSetup = default(AvatarCustomSkinningComponent.MaterialSetup);
                return false;
            }

            (Material avatarMaterial, TextureArraySlot?[] slots) = facialTextures != null
                ? DoFacialFeature(poolMaterialSetup, facialTextures, facialColor, defaultSlotIndexUsed)
                : SetupRegularMaterial(originalMaterial, poolMaterialSetup);

            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastWearableVertCount);
            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;

            DebugMaterial(originalMaterial, avatarMaterial, meshRenderer);

            materialSetup = new AvatarCustomSkinningComponent.MaterialSetup(slots, avatarMaterial, shaderId);
            return true;
        }

        private static (Material avatarMaterial, TextureArraySlot?[] slots) SetupRegularMaterial(
            Material originalMaterial, in PoolMaterialSetup poolMaterialSetup)
        {
            Material avatarMaterial = poolMaterialSetup.Pool.Get();

            var baseColor = originalMaterial.GetColor(BASE_COLOR);
            avatarMaterial.SetColor(BASE_COLOR, baseColor);
            avatarMaterial.renderQueue = (int)RenderQueue.Geometry;

            var emissionColor = originalMaterial.GetColor("_EmissionColor");
            avatarMaterial.SetColor("_Emissive_Color", emissionColor);

            avatarMaterial.DisableKeyword("_IS_CLIPPING_MODE");
            avatarMaterial.DisableKeyword("_IS_CLIPPING_TRANSMODE");
            avatarMaterial.SetInt(Z_WRITE_MODE, 1);

            if (originalMaterial.IsKeywordEnabled("_ALPHATEST_ON") || originalMaterial.GetFloat(ALPHA_CLIP) > 0)
                ConfigureAlphaTest(originalMaterial, avatarMaterial, baseColor);
            else if (originalMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || originalMaterial.GetFloat(SURFACE) > 0)
                ConfigureTransparent(originalMaterial, avatarMaterial, baseColor);

            TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer.SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
            return (avatarMaterial, slots);
        }

        private static void ConfigureAlphaTest(Material originalMaterial, Material avatarMaterial, Color baseColor)
        {
            avatarMaterial.EnableKeyword("_IS_CLIPPING_MODE");
            avatarMaterial.DisableKeyword("_IS_CLIPPING_TRANSMODE");
            avatarMaterial.SetFloat(TWEAK_TRANSPARENCY, 0.0f - (1.0f - baseColor.a));
            avatarMaterial.SetFloat(CLIPPING_LEVEL, originalMaterial.GetFloat(CUTOFF));
            avatarMaterial.SetInt(Z_WRITE_MODE, 1);
            avatarMaterial.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static void ConfigureTransparent(Material originalMaterial, Material avatarMaterial, Color baseColor)
        {
            avatarMaterial.DisableKeyword("_IS_CLIPPING_MODE");
            avatarMaterial.EnableKeyword("_IS_CLIPPING_TRANSMODE");
            avatarMaterial.SetFloat(TWEAK_TRANSPARENCY, 0.0f - (1.0f - baseColor.a));
            avatarMaterial.SetFloat(CLIPPING_LEVEL, originalMaterial.GetFloat(CUTOFF));
            avatarMaterial.SetInt(Z_WRITE_MODE, (int)originalMaterial.GetFloat(Z_WRITE));
            avatarMaterial.SetFloat(SRC_BLEND, originalMaterial.GetFloat(SRC_BLEND));
            avatarMaterial.SetFloat(DST_BLEND, originalMaterial.GetFloat(DST_BLEND));
            avatarMaterial.SetFloat(ALPHA_SRC_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_SRC_BLEND_ORIGINAL));
            avatarMaterial.SetFloat(ALPHA_DST_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_DST_BLEND_ORIGINAL));
            avatarMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        private static bool TryGetFacialFeature(
            Renderer meshRenderer, in AvatarShapeComponent avatarShapeComponent,
            in FacialFeaturesTextures facialFeatures,
            [NotNullWhen(true)] out Dictionary<int, Texture>? textures, out Color color, out int defaultSlotIndexUsed)
        {
            for (var i = 0; i < SUFFIX_CATEGORY_MAP.Length; i++)
            {
                (string suffix, string category, int slotIndexUsed, GetFacialFeatureColor getColor) = SUFFIX_CATEGORY_MAP[i];

                if (meshRenderer.name.EndsWith(suffix))
                {
                    if (!facialFeatures.Value.TryGetValue(category, out textures))
                    {
                        ReportHub.LogError(ReportCategory.AVATAR, $"Facial feature textures are missing for category {category}");
                        break;
                    }

                    color = getColor(in avatarShapeComponent);
                    defaultSlotIndexUsed = slotIndexUsed;
                    return true;
                }
            }

            textures = null;
            color = default(Color);
            defaultSlotIndexUsed = 0;
            return false;
        }

        private static (Material, TextureArraySlot?[]) DoFacialFeature(in PoolMaterialSetup poolMaterialSetup, IReadOnlyDictionary<int, Texture> replacementTexture, Color color, int defaultSlotIndexUsed)
        {
            Material avatarMaterial = poolMaterialSetup.Pool.Get();
            TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer.SetTextures(replacementTexture, avatarMaterial, defaultSlotIndexUsed);
            avatarMaterial.SetColor(BASE_COLOR, color);
            avatarMaterial.SetInt(Z_WRITE_MODE, 0);
            avatarMaterial.renderQueue = (int)RenderQueue.AlphaTest;
            return (avatarMaterial, slots);
        }

        private static void SetAvatarColors(Material avatarMaterial, Material originalMaterial, AvatarShapeComponent avatarShapeComponent)
        {
            // PATO: If this is modified, check DecentralandMaterialGenerator.SetMaterialName,
            // its important for the asset bundles materials to have normalized names but this functionality should work too
            string name = originalMaterial.name;

            if (name.Contains(ComputeShaderConstants.SKIN_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.SkinColor);
            else if (name.Contains(ComputeShaderConstants.HAIR_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                avatarMaterial.SetColor(BASE_COLOR, avatarShapeComponent.HairColor);

            //Force back-face culling for all avatar materials
            avatarMaterial.SetInt(CULL_MODE, 2);
        }
    }
}
