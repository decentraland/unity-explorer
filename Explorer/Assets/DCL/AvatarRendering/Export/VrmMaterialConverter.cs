using System;
using System.Collections.Generic;
using DCL.Diagnostics;
using Runtime.Wearables;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.AvatarRendering.Export
{
    public sealed class VrmMaterialConverter : IDisposable
    {
        private enum BlendModeType { OPAQUE, CUTOUT, TRANSPARENT }

        // DCL/Scene shader properties
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int EMISSION_COLOR = Shader.PropertyToID("_EmissionColor");
        private static readonly int EMISSION_MAP = Shader.PropertyToID("_EmissionMap");
        private static readonly int CULL = Shader.PropertyToID("_Cull");
        private static readonly int ALPHA_CLIP = Shader.PropertyToID("_AlphaClip");
        private static readonly int SURFACE = Shader.PropertyToID("_Surface");
        private static readonly int CUTOFF = Shader.PropertyToID("_Cutoff");

        // MToon shader properties
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int SHADE_TEXTURE = Shader.PropertyToID("_ShadeTexture");
        private static readonly int SHADE_COLOR = Shader.PropertyToID("_ShadeColor");
        private static readonly int CULL_MODE = Shader.PropertyToID("_CullMode");
        private static readonly int BLEND_MODE = Shader.PropertyToID("_BlendMode");
        private static readonly int SRC_BLEND = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND = Shader.PropertyToID("_DstBlend");
        private static readonly int Z_WRITE = Shader.PropertyToID("_ZWrite");
        private static readonly int ALPHA_TO_MASK = Shader.PropertyToID("_AlphaToMask");

        private const string ALPHATEST_ON = "_ALPHATEST_ON";
        private const string ALPHABLEND_ON = "_ALPHABLEND_ON";
        private const string ALPHAPREMULTIPLY_ON = "_ALPHAPREMULTIPLY_ON";

        // Facial feature mesh name suffixes
        private const string EYES_SUFFIX = "Mask_Eyes";
        private const string EYEBROWS_SUFFIX = "Mask_Eyebrows";
        private const string MOUTH_SUFFIX = "Mask_Mouth";

        // Material name patterns for avatar colors
        private const string SKIN_MATERIAL_NAME = "skin";
        private const string HAIR_MATERIAL_NAME = "hair";

        private readonly Shader mtoonShader;
        private readonly List<Material> createdMaterials = new ();
        private readonly List<Texture2D> createdTextures = new ();

        private readonly Color skinColor;
        private readonly Color hairColor;
        private readonly Color eyesColor;

        private readonly Dictionary<string, Texture> facialFeatureMainTextures;
        private readonly Dictionary<string, Texture> facialFeatureMaskTextures;

        public VrmMaterialConverter(
            Color skinColor,
            Color hairColor,
            Color eyesColor,
            Dictionary<string, Texture> facialFeatureMainTextures = null,
            Dictionary<string, Texture> facialFeatureMaskTextures = null)
        {
            this.skinColor = skinColor;
            this.hairColor = hairColor;
            this.eyesColor = eyesColor;
            this.facialFeatureMainTextures = facialFeatureMainTextures ?? new Dictionary<string, Texture>();
            this.facialFeatureMaskTextures = facialFeatureMaskTextures ?? new Dictionary<string, Texture>();

            mtoonShader = Shader.Find("VRM/MToon");

            if (mtoonShader == null)
                ReportHub.LogError(ReportCategory.AVATAR_EXPORT, "VRMaterialConverter: Could not find VRM/MToon shader!");
        }

        public Material[] ConvertMaterials(Material[] sourceMaterials, string meshName)
        {
            var result = new Material[sourceMaterials.Length];

            for (var i = 0; i < sourceMaterials.Length; i++)
            {
                if (meshName.EndsWith(EYES_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.EYES, eyesColor);
                else if (meshName.EndsWith(EYEBROWS_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.EYEBROWS, hairColor);
                else if (meshName.EndsWith(MOUTH_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.MOUTH, skinColor);
                else
                    result[i] = ConvertToMToon(sourceMaterials[i]);

                //createdMaterials.Add(result[i]);
            }

            return result;
        }

        /// <summary>
        /// Converts facial feature material by baking the mask logic into the texture.
        /// Shader formula: albedo * lerp(white, invertedMask.rgb * _BaseColor, invertedMask.a)
        /// Where invertedMask = (1 - mask.rgb, 1 - mask.r)
        /// </summary>
        private Material ConvertFacialFeatureMaterial(string category, Color tintColor)
        {
            var mtoon = new Material(mtoonShader)
            {
                name = category + "_MToon",
            };

            facialFeatureMainTextures.TryGetValue(category, out var mainTex);
            facialFeatureMaskTextures.TryGetValue(category, out var maskTex);

            Texture resultTexture;

            if (mainTex != null && maskTex != null)
            {
                // Bake the shader logic into the texture
                resultTexture = BakeFacialFeatureTexture(mainTex, maskTex, tintColor);
            }
            else if (mainTex != null)
            {
                // No mask - just tint the whole texture
                resultTexture = BakeSimpleTintedTexture(mainTex, tintColor);
            }
            else { resultTexture = null; }

            mtoon.SetTexture(MAIN_TEX, resultTexture);
            mtoon.SetTexture(SHADE_TEXTURE, resultTexture);

            // Color is baked into texture, so use white
            mtoon.SetColor(COLOR, Color.white);
            mtoon.SetColor(SHADE_COLOR, Color.white);

            // Cutout blending for alpha
            SetMToonBlendMode(mtoon, BlendModeType.CUTOUT);
            mtoon.SetFloat(CUTOFF, 0.5f);

            // Double-sided rendering
            mtoon.SetFloat(CULL_MODE, 0);

            return mtoon;
        }

        /// <summary>
        /// Bakes facial feature texture with mask logic:
        /// finalColor = mainTex.rgb * lerp(white, invertedMask.rgb * tintColor, invertedMask.a)
        /// Where invertedMask = (1 - mask.rgb, 1 - mask.r)
        /// </summary>
        private Texture2D BakeFacialFeatureTexture(Texture mainTex, Texture maskTex, Color tintColor)
        {
            int width = mainTex.width;
            int height = mainTex.height;

            // Create readable copies of both textures
            var mainReadable = CreateReadableTexture(mainTex);
            var maskReadable = CreateReadableTexture(maskTex, width, height); // Resize mask to match main

            var bakedTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            var mainPixels = mainReadable.GetPixels();
            var maskPixels = maskReadable.GetPixels();
            var resultPixels = new Color[mainPixels.Length];

            for (var i = 0; i < mainPixels.Length; i++)
            {
                Color main = mainPixels[i];
                Color mask = maskPixels[i];

                // Invert mask
                Color invertedMask = new Color(
                    1f - mask.r,
                    1f - mask.g,
                    1f - mask.b,
                    1f - mask.r // Alpha uses red channel
                );

                // Apply shader formula: main * lerp(white, invertedMask.rgb * tintColor, invertedMask.a)
                Color tinted = new Color(
                    invertedMask.r * tintColor.r,
                    invertedMask.g * tintColor.g,
                    invertedMask.b * tintColor.b,
                    1f
                );

                Color blended = Color.Lerp(Color.white, tinted, invertedMask.a);

                resultPixels[i] = new Color(
                    main.r * blended.r,
                    main.g * blended.g,
                    main.b * blended.b,
                    main.a
                );
            }

            bakedTex.SetPixels(resultPixels);
            bakedTex.Apply();

            UnityObjectUtils.SafeDestroy(mainReadable);
            UnityObjectUtils.SafeDestroy(maskReadable);

            createdTextures.Add(bakedTex);

            return bakedTex;
        }

        /// <summary>
        /// Simple tint when no mask is available - just multiply by tint color.
        /// </summary>
        private Texture2D BakeSimpleTintedTexture(Texture mainTex, Color tintColor)
        {
            int width = mainTex.width;
            int height = mainTex.height;

            var mainReadable = CreateReadableTexture(mainTex);
            var bakedTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            var mainPixels = mainReadable.GetPixels();
            var resultPixels = new Color[mainPixels.Length];

            for (var i = 0; i < mainPixels.Length; i++)
            {
                Color main = mainPixels[i];

                resultPixels[i] = new Color(
                    main.r * tintColor.r,
                    main.g * tintColor.g,
                    main.b * tintColor.b,
                    main.a
                );
            }

            bakedTex.SetPixels(resultPixels);
            bakedTex.Apply();

            UnityObjectUtils.SafeDestroy(mainReadable);

            createdTextures.Add(bakedTex);

            return bakedTex;
        }

        /// <summary>
        /// Creates a readable copy of a texture, optionally resizing it.
        /// </summary>
        private Texture2D CreateReadableTexture(Texture source, int targetWidth = -1, int targetHeight = -1)
        {
            if (targetWidth < 0) targetWidth = source.width;
            if (targetHeight < 0) targetHeight = source.height;

            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            var previousRT = RenderTexture.active;

            Graphics.Blit(source, rt);

            RenderTexture.active = rt;
            var readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readable.Apply();

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }

        private Material ConvertToMToon(Material source)
        {
            if (source == null)
                return CreateDefaultMToon();

            var mtoon = new Material(mtoonShader)
            {
                name = source.name + "_MToon",
            };

            Texture mainTex = GetMainTexture(source);
            Color baseColor = GetBaseColor(source);

            baseColor = ApplyAvatarColor(source.name, baseColor);

            mtoon.SetTexture(MAIN_TEX, mainTex);
            mtoon.SetTexture(SHADE_TEXTURE, mainTex);

            mtoon.SetColor(COLOR, baseColor);
            mtoon.SetColor(SHADE_COLOR, baseColor * 0.85f);

            // Handle emission
            if (source.HasProperty(EMISSION_COLOR))
            {
                Color emissionColor = source.GetColor(EMISSION_COLOR);

                if (emissionColor.maxColorComponent > 0.01f)
                {
                    mtoon.SetColor(EMISSION_COLOR, emissionColor);
                    mtoon.EnableKeyword("_EMISSION");

                    if (source.HasProperty(EMISSION_MAP))
                    {
                        var emissionTex = source.GetTexture(EMISSION_MAP);

                        if (emissionTex != null)
                            mtoon.SetTexture(EMISSION_MAP, emissionTex);
                    }
                }
            }

            float cullMode = source.HasProperty(CULL) ? source.GetFloat(CULL) : 2f;
            mtoon.SetFloat(CULL_MODE, cullMode);

            BlendModeType blendMode = DetermineBlendMode(source);
            SetMToonBlendMode(mtoon, blendMode);

            if (source.HasProperty(CUTOFF))
                mtoon.SetFloat(CUTOFF, source.GetFloat(CUTOFF));

            return mtoon;
        }

        private Color ApplyAvatarColor(string materialName, Color originalColor)
        {
            if (string.IsNullOrEmpty(materialName))
                return originalColor;

            string lowerName = materialName.ToLowerInvariant();

            if (lowerName.Contains(SKIN_MATERIAL_NAME))
                return skinColor;

            if (lowerName.Contains(HAIR_MATERIAL_NAME))
                return hairColor;

            return originalColor;
        }

        private Material CreateDefaultMToon()
        {
            var mtoon = new Material(mtoonShader)
            {
                name = "Default_MToon",
            };

            mtoon.SetColor(COLOR, Color.white);
            mtoon.SetColor(SHADE_COLOR, Color.gray);
            SetMToonBlendMode(mtoon, BlendModeType.OPAQUE);

            return mtoon;
        }

        private Texture GetMainTexture(Material source)
        {
            if (source.HasProperty(BASE_MAP))
            {
                var tex = source.GetTexture(BASE_MAP);

                if (tex != null)
                    return tex;
            }

            return source.mainTexture;
        }

        private Color GetBaseColor(Material source)
        {
            if (source.HasProperty(BASE_COLOR))
                return source.GetColor(BASE_COLOR);

            if (source.HasProperty(COLOR))
                return source.GetColor(COLOR);

            return Color.white;
        }

        private BlendModeType DetermineBlendMode(Material source)
        {
            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
                return BlendModeType.CUTOUT;

            if (source.HasProperty(ALPHA_CLIP) && source.GetFloat(ALPHA_CLIP) > 0)
                return BlendModeType.CUTOUT;

            if (source.HasProperty(SURFACE) && source.GetFloat(SURFACE) > 0)
                return BlendModeType.TRANSPARENT;

            if (source.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                return BlendModeType.TRANSPARENT;

            if (source.renderQueue >= (int)RenderQueue.Transparent)
                return BlendModeType.TRANSPARENT;

            if (source.renderQueue >= (int)RenderQueue.AlphaTest)
                return BlendModeType.CUTOUT;

            return BlendModeType.OPAQUE;
        }

        private void SetMToonBlendMode(Material material, BlendModeType mode)
        {
            switch (mode)
            {
                case BlendModeType.OPAQUE:
                    material.SetFloat(BLEND_MODE, 0);
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetInt(SRC_BLEND, (int)BlendMode.One);
                    material.SetInt(DST_BLEND, (int)BlendMode.Zero);
                    material.SetInt(Z_WRITE, 1);
                    material.SetInt(ALPHA_TO_MASK, 0);
                    material.DisableKeyword(ALPHATEST_ON);
                    material.DisableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.Geometry;
                    break;

                case BlendModeType.CUTOUT:
                    material.SetFloat(BLEND_MODE, 1);
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetInt(SRC_BLEND, (int)BlendMode.One);
                    material.SetInt(DST_BLEND, (int)BlendMode.Zero);
                    material.SetInt(Z_WRITE, 1);
                    material.SetInt(ALPHA_TO_MASK, 1);
                    material.EnableKeyword(ALPHATEST_ON);
                    material.DisableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;

                case BlendModeType.TRANSPARENT:
                    material.SetFloat(BLEND_MODE, 2);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt(SRC_BLEND, (int)BlendMode.SrcAlpha);
                    material.SetInt(DST_BLEND, (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt(Z_WRITE, 0);
                    material.SetInt(ALPHA_TO_MASK, 0);
                    material.DisableKeyword(ALPHATEST_ON);
                    material.EnableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    break;
            }
        }

        public void Dispose()
        {
            foreach (var mat in createdMaterials)
                UnityObjectUtils.SafeDestroy(mat);

            foreach (var tex in createdTextures)
                UnityObjectUtils.SafeDestroy(tex);

            createdMaterials.Clear();
            createdTextures.Clear();
        }
    }
}
