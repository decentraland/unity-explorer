﻿using DCL.Optimization.Pools;
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
        internal const int DEFAULT_SLOT_INDEX = 0;

        internal readonly int arrayID;
        internal readonly int textureID;

        private readonly Dictionary<Vector2Int, TextureArraySlotHandler> handlersByResolution;
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

            handlersByResolution = new Dictionary<Vector2Int, TextureArraySlotHandler>(defaultResolutions.Count);

            //Default resolutions are always squared
            for (var i = 0; i < defaultResolutions.Count; i++)
                CreateHandler(new Vector2Int(defaultResolutions[i], defaultResolutions[i]));
        }


        public TextureArrayHandler(
            IReadOnlyList<TextureArrayResolutionDescriptor> textureArrayResolutionDescriptors,
            int arrayID,
            int textureID,
            TextureFormat textureFormat,
            IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures,
            int arraySizeForMissingResolutions,
            int initialCapacityForEachResolution)
        {
            minArraySize = arraySizeForMissingResolutions;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.textureFormat = textureFormat;
            this.defaultTextures = defaultTextures;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;

            handlersByResolution = new Dictionary<Vector2Int, TextureArraySlotHandler>(textureArrayResolutionDescriptors.Count);

            for (int i = 0; i < textureArrayResolutionDescriptors.Count; i++)
                CreateHandler(textureArrayResolutionDescriptors[i]);
        }

        private TextureArraySlotHandler CreateHandler(TextureArrayResolutionDescriptor descriptor)
        {
            var resolution = new Vector2Int(descriptor.Resolution, descriptor.Resolution);

            var slotHandler = new TextureArraySlotHandler(resolution, descriptor.ArraySize, descriptor.InitialArrayCapacity, textureFormat);
            handlersByResolution[resolution] = slotHandler;

            // When the handler is created initialize the default texture
            if (defaultTextures.TryGetValue(new TextureArrayKey(textureID, resolution), out var defaultTexture))
            {
                var defaultSlot = slotHandler.GetNextFreeSlot();
                Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
            }

            return slotHandler;
        }


        private TextureArraySlotHandler CreateHandler(Vector2Int resolution)
        {
            //We are creating a considerably smaller array for non square resolutions. Shouldn't be a common case
            var slotHandler = new TextureArraySlotHandler(resolution, resolution.x == resolution.y ? minArraySize : minArraySize / 10, initialCapacityForEachResolution, textureFormat);
            handlersByResolution[resolution] = slotHandler;

            // When the handler is created initialize the default texture
            if (defaultTextures.TryGetValue(new TextureArrayKey(textureID, resolution), out var defaultTexture))
            {
                var defaultSlot = slotHandler.GetNextFreeSlot();
                Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
            }

            return slotHandler;
        }

        internal TextureArraySlotHandler GetOrCreateSlotHandler(Vector2Int resolution)
        {
            return handlersByResolution.TryGetValue(resolution, out var slotHandler) ? slotHandler : CreateHandler(resolution);
        }

        public TextureArraySlot SetTexture(Material material, Texture2D texture, Vector2Int resolution)
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

        internal Texture2DArray GetDefaultTextureArray(Vector2Int resolution)
        {
            return GetOrCreateSlotHandler(resolution).arrays[DEFAULT_SLOT_INDEX];
        }

        public void SetDefaultTexture(Material material, int resolution)
        {
            // Default slot is always zero
            // Default textures are always squared
            var defaultSlotArray = GetDefaultTextureArray(new Vector2Int(resolution, resolution));

            material.SetInteger(arrayID, DEFAULT_SLOT_INDEX);
            material.SetTexture(textureID, defaultSlotArray);
        }
    }
}
