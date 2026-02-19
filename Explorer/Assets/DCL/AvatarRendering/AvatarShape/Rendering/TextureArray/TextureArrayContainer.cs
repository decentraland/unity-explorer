using System;
using System.Collections.Generic;
using DCL.Utility;
using Temp.Helper.WebClient;
using UnityEngine;
using UnityEngine.Pool;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayContainer
    {
        private readonly ObjectPool<TextureArraySlot?[]> slotsPool;

        internal readonly IReadOnlyList<TextureArrayMapping> mappings;
        internal int count => mappings.Count;

        public TextureArrayContainer(IReadOnlyList<TextureArrayMapping> mappings)
        {
            this.mappings = mappings;

            slotsPool = new (
                () => new TextureArraySlot?[this.mappings.Count], actionOnGet: array => Array.Clear(array, 0, array.Length), defaultCapacity: 500);
        }

        public void ReleaseSlots(TextureArraySlot?[] slots)
        {
            slotsPool.Release(slots);
        }

        /// <summary>
        ///     Sets textures from every mapping
        /// </summary>
        public TextureArraySlot?[] SetTexturesFromOriginalMaterial(Material originalMaterial, Material targetMaterial)
        {
            TextureArraySlot?[] results = slotsPool.Get();

            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];
                var tex = originalMaterial.GetTexture(mapping.OriginalTextureID) as Texture2D;
                var handlerFormat = mapping.Handler.GetTextureFormat();
                string domain = mapping.Handler.domain;

                if (tex && IsTextureFormatCompatible(tex.format, handlerFormat))
                {
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex, new Vector2Int(tex.width, tex.height));
                    WebGLDebugLog.Log("TextureArrayContainer.SetTexturesFromOriginalMaterial", "SetTexture", $"domain={domain} prop={mapping.OriginalTextureID} texFmt={tex.format}", "H5");
                }
                else if (tex && handlerFormat == TextureFormat.RGBA32)
                {
                    // WebGL: DXT1Crunched cannot be copied; convert to RGBA32 (Avatar_Toon + Raw_GLTF)
                    Texture2D converted = TextureUtilities.EnsureRGBA32Format(tex);
                    try
                    {
                        results[i] = mapping.Handler.SetTexture(targetMaterial, converted, new Vector2Int(converted.width, converted.height));
                        WebGLDebugLog.Log("TextureArrayContainer.SetTexturesFromOriginalMaterial", "SetTextureConverted", $"domain={domain} origFmt={tex.format} -> RGBA32", "H5");
                    }
                    finally
                    {
                        Object.Destroy(converted);
                    }
                }
                else
                {
                    string reason = tex == null ? "texNull" : $"fmtMismatch tex={tex!.format} want={handlerFormat}";
                    WebGLDebugLog.Log("TextureArrayContainer.SetTexturesFromOriginalMaterial", "fallback", $"domain={domain} prop={mapping.OriginalTextureID} {reason} -> SetDefault", "H3,H5");
                    if (IsFormatValidForDefaultTex(handlerFormat))
                        mapping.Handler.SetDefaultTexture(targetMaterial, mapping.DefaultFallbackResolution);
                }
            }

            return results;
        }

        public TextureArraySlot?[] SetTextures(IReadOnlyDictionary<int, Texture> textures, Material targetMaterial, int defaultSlotIndexUsed = 0)
        {
            TextureArraySlot?[] results = slotsPool.Get();

            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];
                var handlerFormat = mapping.Handler.GetTextureFormat();
                bool foundTexture = textures.TryGetValue(mapping.OriginalTextureID, out var texture);
                Texture2D tex = texture as Texture2D;

                if (foundTexture && tex != null && IsTextureFormatCompatible(tex.format, handlerFormat))
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex, new Vector2Int(tex.width, tex.height));
                else if (IsFormatValidForDefaultTex(handlerFormat))
                   mapping.Handler.SetDefaultTexture(targetMaterial, mapping.DefaultFallbackResolution, defaultSlotIndexUsed);
            }

            return results;
        }

        private bool IsFormatValidForDefaultTex(TextureFormat texFormat) =>
            texFormat == DEFAULT_BASEMAP_TEXTURE_FORMAT
            || texFormat == DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT
            || texFormat == DEFAULT_NORMALMAP_TEXTURE_FORMAT
            || texFormat == DEFAULT_WEBGL_TEXTURE_FORMAT;

        /// <summary>
        ///     Exact format match. DXT1Crunched etc. must go through conversion branch (RGBA32).
        /// </summary>
        private static bool IsTextureFormatCompatible(TextureFormat texFormat, TextureFormat handlerFormat) =>
            texFormat == handlerFormat;
    }
}
