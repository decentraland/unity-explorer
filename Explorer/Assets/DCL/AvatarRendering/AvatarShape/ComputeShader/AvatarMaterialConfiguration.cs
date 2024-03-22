using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Helpers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public static class AvatarMaterialConfiguration
    {
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int CLIPPING_LEVEL = Shader.PropertyToID("_Clipping_Level");
        private static readonly int CULL_MODE = Shader.PropertyToID("_CullMode");

        private static readonly int Z_WRITE_MODE = Shader.PropertyToID("_ZWriteMode");
        private static readonly int CULL = Shader.PropertyToID("_Cull");
        private static readonly int Z_WRITE = Shader.PropertyToID("_ZWrite");

        private const float DEFAULT_CLIPPING_LEVEL = 0.49f;

        private delegate Color GetFacialFeatureColor(in AvatarShapeComponent shapeComponent);

        private static readonly (string suffix, string category, GetFacialFeatureColor getColor)[] SUFFIX_CATEGORY_MAP =
        {
            ("eyes", WearablesConstants.Categories.EYES, (in AvatarShapeComponent shape) => shape.EyesColor),
            ("eyebrows", WearablesConstants.Categories.EYEBROWS, (in AvatarShapeComponent shape) => shape.HairColor),
            ("mouth", WearablesConstants.Categories.MOUTH, (in AvatarShapeComponent shape) => shape.SkinColor)
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
            {
                shaderId = TextureArrayConstants.SHADERID_DCL_TOON;
                var poolMaterialSetup = poolHandler.GetMaterialPool(shaderId);
                avatarMaterial = poolMaterialSetup.Pool.Get();

                if (originalMaterial.IsKeywordEnabled("_ALPHATEST_ON"))
                {
                    avatarMaterial.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                    avatarMaterial.SetFloat(CLIPPING_LEVEL, DEFAULT_CLIPPING_LEVEL);
                }


                slots = poolMaterialSetup.TextureArrayContainer.SetTexturesFromOriginalMaterial(originalMaterial, avatarMaterial);
            }

            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastWearableVertCount);
            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;

            return new AvatarCustomSkinningComponent.MaterialSetup(slots, avatarMaterial, shaderId);
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
            return default;
        }

        private static (Material, TextureArraySlot?[], int) DoFacialFeature(IAvatarMaterialPoolHandler poolHandler, IReadOnlyDictionary<int, Texture> replacementTexture, Color color)
        {
            var poolMaterialSetup = poolHandler.GetMaterialPool(TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES);
            var avatarMaterial = poolMaterialSetup.Pool.Get();
            var slots = poolMaterialSetup.TextureArrayContainer.SetTextures(replacementTexture, avatarMaterial);
            avatarMaterial.SetColor(BASE_COLOR, color);
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

            avatarMaterial.renderQueue = originalMaterial.renderQueue;
            avatarMaterial.SetInt(CULL_MODE, (int)originalMaterial.GetFloat(CULL));
            avatarMaterial.SetInt(Z_WRITE_MODE, (int)originalMaterial.GetFloat(Z_WRITE));
        }
    }
}
