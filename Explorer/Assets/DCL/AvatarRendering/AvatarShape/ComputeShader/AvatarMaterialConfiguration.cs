using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Helpers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public static class AvatarMaterialConfiguration
    {
        private delegate Color GetFacialFeatureColor(in AvatarShapeComponent shapeComponent);

        private const float DEFAULT_CLIPPING_LEVEL = 0.49f;
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int CLIPPING_LEVEL = Shader.PropertyToID("_Clipping_Level");
        private static readonly int CULL_MODE = Shader.PropertyToID("_CullMode");

        private static readonly int Z_WRITE_MODE = Shader.PropertyToID("_ZWriteMode");
        private static readonly int CULL = Shader.PropertyToID("_Cull");
        private static readonly int Z_WRITE = Shader.PropertyToID("_ZWrite");
        private static readonly int ALPHA_CLIP = Shader.PropertyToID("_AlphaClip");
        private static readonly int TWEAK_TRANSPARENCY = Shader.PropertyToID("_Tweak_transparency");
        private static readonly int CUTOFF = Shader.PropertyToID("_Cutoff");
        private static readonly int SURFACE = Shader.PropertyToID("_Surface");

        private static readonly (string suffix, string category, GetFacialFeatureColor getColor)[] SUFFIX_CATEGORY_MAP =
        {
            ("eyes", WearablesConstants.Categories.EYES, (in AvatarShapeComponent shape) => shape.EyesColor),
            ("eyebrows", WearablesConstants.Categories.EYEBROWS, (in AvatarShapeComponent shape) => shape.HairColor),
            ("mouth", WearablesConstants.Categories.MOUTH, (in AvatarShapeComponent shape) => shape.SkinColor),
        };

        public static AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler poolHandler,
            AvatarShapeComponent avatarShapeComponent,
            in FacialFeaturesTextures facialFeatures)
        {
            TextureArraySlot?[] slots;
            Material avatarMaterial;
            int shaderId;

            (avatarMaterial, slots, shaderId) = TrySetupFacialFeature(meshRenderer, poolHandler, avatarShapeComponent, facialFeatures, out bool isFacialFeature);

            if (!isFacialFeature)
                (avatarMaterial, slots, shaderId) = SetupRegularMaterial(originalMaterial, poolHandler);

            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastWearableVertCount);
            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;

            return new AvatarCustomSkinningComponent.MaterialSetup(slots, avatarMaterial, shaderId);
        }

        private static (Material avatarMaterial, TextureArraySlot?[] slots, int shaderId) SetupRegularMaterial(
            Material originalMaterial, IAvatarMaterialPoolHandler poolHandler)
        {
            int shaderId = TextureArrayConstants.SHADERID_DCL_TOON;
            PoolMaterialSetup poolMaterialSetup = poolHandler.GetMaterialPool(shaderId);
            Material avatarMaterial = poolMaterialSetup.Pool.Get();

            if (originalMaterial.IsKeywordEnabled("_ALPHATEST_ON") || originalMaterial.GetFloat(ALPHA_CLIP) > 0)
                ConfigureAlphaTest(originalMaterial, avatarMaterial);
            else if (originalMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || originalMaterial.GetFloat(SURFACE) > 0)
                ConfigureTransparent(originalMaterial, avatarMaterial);

            TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer.SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
            return (avatarMaterial, slots, shaderId);
        }

        private static void ConfigureTransparent(Material originalMaterial, Material avatarMaterial)
        {
            avatarMaterial.DisableKeyword("_IS_CLIPPING_MODE");
            avatarMaterial.EnableKeyword("_IS_CLIPPING_TRANSMODE");
            Color baseColour = originalMaterial.GetColor(BASE_COLOR);
            avatarMaterial.SetFloat(TWEAK_TRANSPARENCY, 0.0f - (1.0f - baseColour.a));
            avatarMaterial.SetFloat(CLIPPING_LEVEL, originalMaterial.GetFloat(CUTOFF));
            avatarMaterial.SetInt(Z_WRITE_MODE, 0);
            avatarMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ConfigureAlphaTest(Material originalMaterial, Material avatarMaterial)
        {
            avatarMaterial.EnableKeyword("_IS_CLIPPING_MODE");
            avatarMaterial.DisableKeyword("_IS_CLIPPING_TRANSMODE");
            Color baseColour = originalMaterial.GetColor(BASE_COLOR);
            avatarMaterial.SetFloat(TWEAK_TRANSPARENCY, 1.0f - baseColour.a);
            avatarMaterial.SetFloat(CLIPPING_LEVEL, originalMaterial.GetFloat(CUTOFF));
            avatarMaterial.SetInt(Z_WRITE_MODE, 1);
            avatarMaterial.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static (Material, TextureArraySlot?[], int) TrySetupFacialFeature(
            Renderer meshRenderer, IAvatarMaterialPoolHandler poolHandler, in AvatarShapeComponent avatarShapeComponent,
            in FacialFeaturesTextures facialFeatures,
            out bool result)
        {
            for (var i = 0; i < SUFFIX_CATEGORY_MAP.Length; i++)
            {
                (string suffix, string category, GetFacialFeatureColor getColor) = SUFFIX_CATEGORY_MAP[i];

                if (meshRenderer.name.Contains(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return DoFacialFeature(poolHandler, facialFeatures.Value[category], getColor(in avatarShapeComponent));
                }
            }

            result = false;
            return default((Material, TextureArraySlot?[], int));
        }

        private static (Material, TextureArraySlot?[], int) DoFacialFeature(IAvatarMaterialPoolHandler poolHandler, IReadOnlyDictionary<int, Texture> replacementTexture, Color color)
        {
            PoolMaterialSetup poolMaterialSetup = poolHandler.GetMaterialPool(TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES);
            Material avatarMaterial = poolMaterialSetup.Pool.Get();
            TextureArraySlot?[] slots = poolMaterialSetup.TextureArrayContainer.SetTextures(replacementTexture, avatarMaterial);
            avatarMaterial.SetColor(BASE_COLOR, color);
            avatarMaterial.SetInt(Z_WRITE_MODE, 0);
            avatarMaterial.renderQueue = (int)RenderQueue.AlphaTest;
            return (avatarMaterial, slots, TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES);
        }

        private static void SetAvatarColors(Material avatarMaterial, Material originalMaterial, AvatarShapeComponent avatarShapeComponent)
        {
            // PATO: If this is modified, check DecentralandMaterialGenerator.SetMaterialName,
            // its important for the asset bundles materials to have normalized names but this functionality should work too
            string name = originalMaterial.name;

            if (name.Contains(ComputeShaderConstants.SKIN_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.SkinColor);
            else if (name.Contains(ComputeShaderConstants.HAIR_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.HairColor);

            avatarMaterial.SetInt(CULL_MODE, (int)originalMaterial.GetFloat(CULL));
        }
    }
}
