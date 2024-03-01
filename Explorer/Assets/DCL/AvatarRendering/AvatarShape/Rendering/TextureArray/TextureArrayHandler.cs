using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayHandler
    {
        internal readonly Dictionary<int, TextureArrayResolution> resolutionDictionary;
        private readonly int initialCapacityForEachResolution;
        private readonly int minArraySize;
        private readonly int arrayID;
        private readonly int textureID;
        private readonly TextureFormat textureFormat;
        private readonly int nResolution;

        public TextureArrayHandler(int minArraySize, int arrayID, int textureID, int _nResolution, TextureFormat _textureFormat, int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT)
        {
            resolutionDictionary = new Dictionary<int, TextureArrayResolution>();
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;
            this.textureFormat = _textureFormat;
            this.nResolution = _nResolution;

            //We initialize some default values
            resolutionDictionary.Add(_nResolution, new TextureArrayResolution(_nResolution, minArraySize, initialCapacityForEachResolution, textureFormat));
            //resolutionDictionary.Add(512, new TextureArrayResolution(256, minArraySize, initialCapacityForEachResolution, textureFormat));
            //resolutionDictionary.Add(256, new TextureArrayResolution(512, minArraySize, initialCapacityForEachResolution, textureFormat));
        }

        public TextureArraySlot SetTexture(Material material, Texture2D texture)
        {
            //We only support square textures
            int resolution = texture.width;

            //TODO: Resolution are not fixed for face features
            //if (!resolutionDictionary.ContainsKey(resolution))
            //     resolutionDictionary.Add(resolution, new TextureArrayResolution(resolution, minArraySize, initialCapacityForEachResolution, textureFormat));

            TextureArraySlot slot = resolutionDictionary[resolution].GetNextFreeSlot();
            Debug.Log(slot.TextureArray.format);
            int mipLevel = 0;
            //for (int mipLevel = 0; mipLevel < texture.mipmapCount; ++mipLevel)
            {
                Graphics.CopyTexture(texture, srcElement: 0, srcMip: mipLevel, slot.TextureArray, dstElement: slot.UsedSlotIndex, dstMip: mipLevel);
            }
            material.SetInteger(arrayID, slot.UsedSlotIndex);
            material.SetTexture(textureID, slot.TextureArray);
            return slot;
        }
    }
}
