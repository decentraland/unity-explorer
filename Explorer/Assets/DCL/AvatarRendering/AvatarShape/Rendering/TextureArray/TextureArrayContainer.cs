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
        public TextureArraySlot?[] SetTexturesFromOriginalMaterial(Material originalMaterial, Material targetMaterial, int _nShaderID)
        {
            TextureArraySlot?[] results = TextureArrayContainerFactory.SLOTS_POOL.Get();

            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];

                if (mapping.nShaderID == _nShaderID)
                {
                    // Check if the texture is present in the original material
                    var tex = originalMaterial.GetTexture(mapping.OriginalTextureID) as Texture2D;

                    if (tex )
                    {
                        results[i] = mapping.Handler.SetTexture(targetMaterial, tex);
                    }
                    else if (mapping.OriginalTextureID == TextureArrayConstants.BUMP_MAP_ORIGINAL_TEXTURE_ID)
                    {
                        Texture2D tex_temp = Resources.Load<Texture2D>("TempTextures/FlatNormal");
                        if (tex_temp)
                            results[i] = mapping.Handler.SetTexture(targetMaterial, tex_temp);
                    }
                    else if (mapping.OriginalTextureID == TextureArrayConstants.EMISSION_MAP_ORIGINAL_TEXTURE_ID)
                    {
                        Texture2D tex_temp = Resources.Load<Texture2D>("TempTextures/DefaultBlack");
                        if (tex_temp)
                            results[i] = mapping.Handler.SetTexture(targetMaterial, tex_temp);
                    }
                }
            }

            return results;
        }

        public TextureArraySlot?[] SetTexturesFromOriginalMaterial(Material originalMaterial, Texture2D texture, Material targetMaterial, int _nShaderID)
        {
            TextureArraySlot?[] results = TextureArrayContainerFactory.SLOTS_POOL.Get();
            for (var i = 0; i < mappings.Count; i++)
            {
                TextureArrayMapping mapping = mappings[i];

                if (mapping.nShaderID == _nShaderID)
                {
                    // Check if the texture is present in the original material
                    var tex = originalMaterial.GetTexture(mapping.OriginalTextureID) as Texture2D;

                    if (tex)
                    {
                        results[i] = mapping.Handler.SetTexture(targetMaterial, texture);
                    }
                    else if (mapping.OriginalTextureID == TextureArrayConstants.BUMP_MAP_ORIGINAL_TEXTURE_ID)
                    {
                        Texture2D tex_temp = Resources.Load<Texture2D>("TempTextures/FlatNormal");
                        if (tex_temp)
                            results[i] = mapping.Handler.SetTexture(targetMaterial, tex_temp);
                    }
                    else if (mapping.OriginalTextureID == TextureArrayConstants.EMISSION_MAP_ORIGINAL_TEXTURE_ID)
                    {
                        Texture2D tex_temp = Resources.Load<Texture2D>("TempTextures/DefaultBlack");
                        if (tex_temp)
                            results[i] = mapping.Handler.SetTexture(targetMaterial, tex_temp);
                    }
                }

            }
            return results;

        }
    }
}
