using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayContainer
    {
        internal readonly IReadOnlyList<TextureArrayMapping> mappings;
        internal int count => mappings.Count;

        public TextureArrayContainer(IReadOnlyList<TextureArrayMapping> mappings)
        {
            this.mappings = mappings;
        }

        /// <summary>
        ///     Sets textures from every mapping
        /// </summary>
        public TextureArraySlot?[] SetTexturesFromOriginalMaterial(Material originalMaterial, Material targetMaterial)
        {
            TextureArraySlot?[] results = TextureArrayContainerFactory.SLOTS_POOL.Get();

            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];
                // Check if the texture is present in the original material
                var tex = originalMaterial.GetTexture(mapping.OriginalTextureID) as Texture2D;
                if (tex)
                    results[i] = mapping.Handler.SetTexture(targetMaterial, tex);
                else
                   mapping.Handler.SetDefaultTexture(targetMaterial);
            }

            return results;
        }

        public TextureArraySlot?[] SetTexturesFromOriginalMaterial(int mappingID, Texture2D texture, Material targetMaterial)
        {
            TextureArraySlot?[] results = TextureArrayContainerFactory.SLOTS_POOL.Get();
            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];
                if(mapping.OriginalTextureID == mappingID)
                    results[i] = mapping.Handler.SetTexture(targetMaterial, texture);
            }
            return results;

        }
    }
}
