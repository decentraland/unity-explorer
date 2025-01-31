using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

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
                // Check if the texture is present in the original material
                var tex = originalMaterial.GetTexture(mapping.OriginalTextureID) as Texture2D;
                var handlerFormat = mapping.Handler.GetTextureFormat();
                if (tex && tex.format == handlerFormat)
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex, new Vector2Int(tex.width, tex.height));
                else if (tex == null
                         || handlerFormat == DEFAULT_BASEMAP_TEXTURE_FORMAT
                         || handlerFormat == DEFAULT_NORMALMAP_TEXTURE_FORMAT
                         || handlerFormat == DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT)
                    mapping.Handler.SetDefaultTexture(targetMaterial, mapping.DefaultFallbackResolution);
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

                if (foundTexture && tex!.format == handlerFormat)
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex, new Vector2Int(tex.width, tex.height));
                else if (tex == null
                         || handlerFormat == DEFAULT_BASEMAP_TEXTURE_FORMAT
                         || handlerFormat == DEFAULT_NORMALMAP_TEXTURE_FORMAT
                         || handlerFormat == DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT)
                   mapping.Handler.SetDefaultTexture(targetMaterial, mapping.DefaultFallbackResolution, defaultSlotIndexUsed);
            }

            return results;
        }
    }
}
