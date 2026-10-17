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
        internal const int DEFAULT_SLOT_INDEX = 0;
        internal readonly int arrayID;
        internal readonly int textureID;

        private readonly Dictionary<Vector2Int, TextureArraySlotHandler> handlersByResolution;
        private readonly int minArraySize;
        private readonly int effectiveMinArraySizeForLowRes;
        private readonly int effectiveMinArraySizeForHighRes;
        private readonly int initialCapacityForEachResolution;
        private readonly TextureFormat textureFormat;

        private readonly IReadOnlyDictionary<TextureArrayKey, Texture>? defaultTextures;

        private readonly string domain;

        /// <summary>
        /// Used for Avatar texture arrays
        /// where texture array size is different for high-res textures
        /// </summary>
        public TextureArrayHandler(
            string domain,
            int minArraySize,
            int arrayID,
            int textureID,
            IReadOnlyList<int> defaultResolutions,
            TextureFormat textureFormat,
            IReadOnlyDictionary<TextureArrayKey, Texture>? defaultTextures = null,
            int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT,
            int? minArraySizeForHighRes = null)
        {
            this.minArraySize = minArraySize;
            this.domain = domain;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.textureFormat = textureFormat;
            this.defaultTextures = defaultTextures;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;
            
            // NOTE: texture array size is different (smaller) for high-resolution textures
            effectiveMinArraySizeForLowRes = minArraySize;
            effectiveMinArraySizeForHighRes = minArraySizeForHighRes ?? minArraySize;
            
            handlersByResolution = new Dictionary<Vector2Int, TextureArraySlotHandler>(defaultResolutions.Count);

            //NOTE: Default resolutions are always squared
            for (var i = 0; i < defaultResolutions.Count; i++)
                CreateHandler(new Vector2Int(defaultResolutions[i], defaultResolutions[i]));
        }


        /// <summary>
        /// Used for Scene_LOD when we have texture array descriptors
        /// </summary>
        public TextureArrayHandler(
            string domain,
            IReadOnlyList<TextureArrayResolutionDescriptor> textureArrayResolutionDescriptors,
            int arrayID,
            int textureID,
            TextureFormat textureFormat,
            IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures,
            int arraySizeForMissingResolutions,
            int initialCapacityForEachResolution)
        {
            minArraySize = arraySizeForMissingResolutions;
            this.domain = domain;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.textureFormat = textureFormat;
            this.defaultTextures = defaultTextures;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;
            
            // NOTE: texture array size is always the same for all resolutions
            effectiveMinArraySizeForLowRes = minArraySize;
            effectiveMinArraySizeForHighRes = minArraySize;
            
            handlersByResolution = new Dictionary<Vector2Int, TextureArraySlotHandler>(textureArrayResolutionDescriptors.Count);

            for (int i = 0; i < textureArrayResolutionDescriptors.Count; i++)
                CreateHandler(textureArrayResolutionDescriptors[i]);
        }

        private TextureArraySlotHandler CreateHandler(TextureArrayResolutionDescriptor descriptor)
        {
            var resolution = new Vector2Int(descriptor.Resolution, descriptor.Resolution);

            var slotHandler = new TextureArraySlotHandler(domain, resolution, descriptor.ArraySize, descriptor.InitialArrayCapacity, textureFormat);
            handlersByResolution[resolution] = slotHandler;

            // When the handler is created initialize the default texture
            var count = defaultTextures?.Count ?? 0;
            for (int i = 0; i < count; ++i)
            {
                if (defaultTextures!.TryGetValue(new TextureArrayKey(textureID, resolution, i), out var defaultTexture))
                {
                    var defaultSlot = slotHandler.GetNextFreeSlot();
                    Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
                }
            }

            return slotHandler;
        }

        private TextureArraySlotHandler CreateHandler(Vector2Int resolution)
        {
            // NOTE: We are creating a considerably smaller array for resolutions over
            // NOTE: the high-resolution threshold. Shouldn't be a common case
            int baseSizeToUse = resolution.x >= TextureArrayConstants.HIGH_RES_THRESHOLD || 
                                resolution.y >= TextureArrayConstants.HIGH_RES_THRESHOLD
                ? effectiveMinArraySizeForHighRes
                : effectiveMinArraySizeForLowRes;

            // NOTE: We are creating a considerably smaller array for
            // NOTE: non-square resolutions. Shouldn't be a common case
            int finalSizeForSlotHandler = resolution.x == resolution.y
                ? baseSizeToUse
                : baseSizeToUse / 10;

            
            var slotHandler = new TextureArraySlotHandler(
                domain,
                resolution,
                finalSizeForSlotHandler,
                initialCapacityForEachResolution,
                textureFormat);
            
            handlersByResolution[resolution] = slotHandler;

            // When the handler is created, initialize the default texture
            if (defaultTextures != null)
            {
                for (int i = 0; i < defaultTextures.Count; ++i)
                {
                    if (defaultTextures.TryGetValue(new TextureArrayKey(textureID, resolution, i), out var defaultTexture))
                    {
                        var defaultSlot = slotHandler.GetNextFreeSlot();
                        Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
                    }
                }
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

        public void SetDefaultTexture(Material material, int resolution, int defaultSlotIndexUsed = 0)
        {
            // Default slot is always zero
            // Default textures are always squared
            var defaultSlotArray = GetDefaultTextureArray(new Vector2Int(resolution, resolution));

            material.SetInteger(arrayID, defaultSlotIndexUsed);
            material.SetTexture(textureID, defaultSlotArray);
        }

        public TextureFormat GetTextureFormat() =>
            textureFormat;

    }
}
