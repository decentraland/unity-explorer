using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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
                if (tex)
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex, tex.width);
                else
                   mapping.Handler.SetDefaultTexture(targetMaterial, TextureArrayConstants.MAIN_TEXTURE_RESOLUTION);
            }

            return results;
        }

        public TextureArraySlot?[] SetTextures(IReadOnlyDictionary<int, Texture> textures, Material targetMaterial)
        {
            TextureArraySlot?[] results = slotsPool.Get();

            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];

                if (textures.TryGetValue(mapping.OriginalTextureID, out var texture))
                    results[i] = mapping.Handler.SetTexture(targetMaterial, texture as Texture2D, texture.width);
                else
                    mapping.Handler.ResetTexture(targetMaterial);
            }

            return results;
        }
    }
}
