using System.Collections.Generic;
using DCL.Diagnostics;
using Runtime.Wearables;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.AvatarRendering.Export
{
    public class VRMaterialConverter
    {
        private enum BlendModeType { Opaque, Cutout, Transparent }
        
        // DCL/Scene shader properties
        private static readonly int _BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int _BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int _EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _EmissionMap = Shader.PropertyToID("_EmissionMap");
        private static readonly int _Cull = Shader.PropertyToID("_Cull");
        private static readonly int _AlphaClip = Shader.PropertyToID("_AlphaClip");
        private static readonly int _Surface = Shader.PropertyToID("_Surface");
        private static readonly int _Cutoff = Shader.PropertyToID("_Cutoff");

        // MToon shader properties
        private static readonly int _Color = Shader.PropertyToID("_Color");
        private static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int _ShadeTexture = Shader.PropertyToID("_ShadeTexture");
        private static readonly int _ShadeColor = Shader.PropertyToID("_ShadeColor");
        private static readonly int _CullMode = Shader.PropertyToID("_CullMode");
        private static readonly int _BlendMode = Shader.PropertyToID("_BlendMode");
        private static readonly int _SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int _DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int _ZWrite = Shader.PropertyToID("_ZWrite");
        private static readonly int _AlphaToMask = Shader.PropertyToID("_AlphaToMask");

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
        private readonly List<Material> createdMaterials = new();
        private readonly List<Texture2D> createdTextures = new();

        private readonly Color skinColor;
        private readonly Color hairColor;
        private readonly Color eyesColor;
        
        private readonly Dictionary<string, Texture> facialFeatureMainTextures;
        private readonly Dictionary<string, Texture> facialFeatureMaskTextures;
        

        public VRMaterialConverter(
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

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                if (meshName.EndsWith(EYES_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.EYES, eyesColor);
                else if (meshName.EndsWith(EYEBROWS_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.EYEBROWS, hairColor);
                else if (meshName.EndsWith(MOUTH_SUFFIX))
                    result[i] = ConvertFacialFeatureMaterial(WearableCategories.Categories.MOUTH, skinColor);
                else
                    result[i] = ConvertToMToon(sourceMaterials[i]);

                createdMaterials.Add(result[i]);
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
            var mtoon = new Material(mtoonShader);
            mtoon.name = category + "_MToon";

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
            else
            {
                resultTexture = null;
            }

            mtoon.SetTexture(_MainTex, resultTexture);
            mtoon.SetTexture(_ShadeTexture, resultTexture);

            // Color is baked into texture, so use white
            mtoon.SetColor(_Color, Color.white);
            mtoon.SetColor(_ShadeColor, Color.white);

            // Cutout blending for alpha
            SetMToonBlendMode(mtoon, BlendModeType.Cutout);
            mtoon.SetFloat(_Cutoff, 0.5f);

            // Double-sided rendering
            mtoon.SetFloat(_CullMode, 0);

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

            for (int i = 0; i < mainPixels.Length; i++)
            {
                Color main = mainPixels[i];
                Color mask = maskPixels[i];

                // Invert mask
                Color invertedMask = new Color(
                    1f - mask.r,
                    1f - mask.g,
                    1f - mask.b,
                    1f - mask.r  // Alpha uses red channel
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

            for (int i = 0; i < mainPixels.Length; i++)
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

        public Material ConvertToMToon(Material source)
        {
            if (source == null)
                return CreateDefaultMToon();

            var mtoon = new Material(mtoonShader);
            mtoon.name = source.name + "_MToon";

            Texture mainTex = GetMainTexture(source);
            Color baseColor = GetBaseColor(source);

            baseColor = ApplyAvatarColor(source.name, baseColor);

            mtoon.SetTexture(_MainTex, mainTex);
            mtoon.SetTexture(_ShadeTexture, mainTex);

            mtoon.SetColor(_Color, baseColor);
            mtoon.SetColor(_ShadeColor, baseColor * 0.85f);

            // Handle emission
            if (source.HasProperty(_EmissionColor))
            {
                Color emissionColor = source.GetColor(_EmissionColor);
                if (emissionColor.maxColorComponent > 0.01f)
                {
                    mtoon.SetColor(_EmissionColor, emissionColor);
                    mtoon.EnableKeyword("_EMISSION");

                    if (source.HasProperty(_EmissionMap))
                    {
                        var emissionTex = source.GetTexture(_EmissionMap);
                        if (emissionTex != null)
                            mtoon.SetTexture(_EmissionMap, emissionTex);
                    }
                }
            }

            float cullMode = source.HasProperty(_Cull) ? source.GetFloat(_Cull) : 2f;
            mtoon.SetFloat(_CullMode, cullMode);

            BlendModeType blendMode = DetermineBlendMode(source);
            SetMToonBlendMode(mtoon, blendMode);

            if (source.HasProperty(_Cutoff))
                mtoon.SetFloat(_Cutoff, source.GetFloat(_Cutoff));

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
            var mtoon = new Material(mtoonShader);
            mtoon.name = "Default_MToon";
            mtoon.SetColor(_Color, Color.white);
            mtoon.SetColor(_ShadeColor, Color.gray);
            SetMToonBlendMode(mtoon, BlendModeType.Opaque);
            
            return mtoon;
        }

        private Texture GetMainTexture(Material source)
        {
            if (source.HasProperty(_BaseMap))
            {
                var tex = source.GetTexture(_BaseMap);
                
                if (tex != null) 
                    return tex;
            }

            return source.mainTexture;
        }

        private Color GetBaseColor(Material source)
        {
            if (source.HasProperty(_BaseColor))
                return source.GetColor(_BaseColor);

            if (source.HasProperty(_Color))
                return source.GetColor(_Color);

            return Color.white;
        }

        private BlendModeType DetermineBlendMode(Material source)
        {
            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
                return BlendModeType.Cutout;

            if (source.HasProperty(_AlphaClip) && source.GetFloat(_AlphaClip) > 0)
                return BlendModeType.Cutout;

            if (source.HasProperty(_Surface) && source.GetFloat(_Surface) > 0)
                return BlendModeType.Transparent;

            if (source.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                return BlendModeType.Transparent;

            if (source.renderQueue >= (int)RenderQueue.Transparent)
                return BlendModeType.Transparent;

            if (source.renderQueue >= (int)RenderQueue.AlphaTest)
                return BlendModeType.Cutout;

            return BlendModeType.Opaque;
        }

        private void SetMToonBlendMode(Material material, BlendModeType mode)
        {
            switch (mode)
            {
                case BlendModeType.Opaque:
                    material.SetFloat(_BlendMode, 0);
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetInt(_SrcBlend, (int)BlendMode.One);
                    material.SetInt(_DstBlend, (int)BlendMode.Zero);
                    material.SetInt(_ZWrite, 1);
                    material.SetInt(_AlphaToMask, 0);
                    material.DisableKeyword(ALPHATEST_ON);
                    material.DisableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.Geometry;
                    break;

                case BlendModeType.Cutout:
                    material.SetFloat(_BlendMode, 1);
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetInt(_SrcBlend, (int)BlendMode.One);
                    material.SetInt(_DstBlend, (int)BlendMode.Zero);
                    material.SetInt(_ZWrite, 1);
                    material.SetInt(_AlphaToMask, 1);
                    material.EnableKeyword(ALPHATEST_ON);
                    material.DisableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;

                case BlendModeType.Transparent:
                    material.SetFloat(_BlendMode, 2);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt(_SrcBlend, (int)BlendMode.SrcAlpha);
                    material.SetInt(_DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt(_ZWrite, 0);
                    material.SetInt(_AlphaToMask, 0);
                    material.DisableKeyword(ALPHATEST_ON);
                    material.EnableKeyword(ALPHABLEND_ON);
                    material.DisableKeyword(ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    break;
            }
        }

        public void Cleanup()
        {
            foreach (var mat in createdMaterials)
                if (mat != null) UnityObjectUtils.SafeDestroy(mat);
            
            foreach (var tex in createdTextures)
                if (tex != null) UnityObjectUtils.SafeDestroy(tex);
            
            createdMaterials.Clear();
            createdTextures.Clear();
        }
    }
}