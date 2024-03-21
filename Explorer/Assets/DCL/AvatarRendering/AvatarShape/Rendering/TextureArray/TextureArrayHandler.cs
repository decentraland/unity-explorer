using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    /// <summary>
    /// Exists in a single instance per {Shader, TextureName} pair.
    /// Handles different resolutions dynamically
    /// </summary>
    public class TextureArrayHandler
    {
        internal readonly int arrayID;
        internal readonly int textureID;

        private readonly Dictionary<int, TextureArraySlotHandler> handlersByResolution;
        private readonly int minArraySize;
        private readonly int initialCapacityForEachResolution;
        private readonly TextureFormat textureFormat;

        private readonly IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures;

        public TextureArrayHandler(
            int minArraySize,
            int arrayID,
            int textureID,
            IReadOnlyList<int> defaultResolutions,
            TextureFormat textureFormat,
            IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures,
            int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT)
        {
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.textureFormat = textureFormat;
            this.defaultTextures = defaultTextures;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;

            handlersByResolution = new Dictionary<int, TextureArraySlotHandler>(defaultResolutions.Count);

            for (var i = 0; i < defaultResolutions.Count; i++)
                CreateHandler(defaultResolutions[i]);
        }

        private TextureArraySlotHandler CreateHandler(int resolution)
        {
            var slotHandler = new TextureArraySlotHandler(resolution, minArraySize, initialCapacityForEachResolution, textureFormat);
            handlersByResolution[resolution] = slotHandler;

            // When the handler is created initialize the default texture
            if (defaultTextures.TryGetValue(new TextureArrayKey(textureID, resolution), out var defaultTexture))
            {
                var defaultSlot = slotHandler.GetNextFreeSlot();
                Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
            }

            return slotHandler;
        }

        private TextureArraySlotHandler GetOrCreateSlotHandler(int resolution) =>
            handlersByResolution.TryGetValue(resolution, out var slotHandler) ? slotHandler : CreateHandler(resolution);

        public TextureArraySlot SetTexture(Material material, Texture2D texture, int resolution)
        {
            TextureArraySlot slot = GetOrCreateSlotHandler(resolution).GetNextFreeSlot();
            var mipLevel = 0;

            //for (int mipLevel = 0; mipLevel < texture.mipmapCount; ++mipLevel)
            //{
            Graphics.CopyTexture(texture, srcElement: 0, srcMip: mipLevel, slot.TextureArray, dstElement: slot.UsedSlotIndex, dstMip: mipLevel);

            //}
            material.SetInteger(arrayID, slot.UsedSlotIndex);
            material.SetTexture(textureID, slot.TextureArray);
            return slot;
        }

        /// <summary>
        /// Used when no default texture is needed
        /// </summary>
        public void ResetTexture(Material material)
        {
            material.SetInteger(arrayID, -1);
            material.SetTexture(textureID, Texture2D.whiteTexture);
        }

        public void SetDefaultTexture(Material material, int resolution)
        {
            // Default slot is always zero

            var defaultSlotArray = GetOrCreateSlotHandler(resolution).arrays[0];

            material.SetInteger(arrayID, 0);
            material.SetTexture(textureID, defaultSlotArray);
        }
    }
}
