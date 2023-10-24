using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayType
    {
        internal readonly Dictionary<int, TextureArrayResolution> resolutionDictionary;
        private readonly int minArraySize;
        private readonly int arrayID;
        private readonly int textureID;

        public TextureArrayType(int minArraySize, int arrayID, int textureID)
        {
            resolutionDictionary = new Dictionary<int, TextureArrayResolution>();
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;

            //We initialize some default values
            resolutionDictionary.Add(256, new TextureArrayResolution(256, minArraySize));
            resolutionDictionary.Add(512, new TextureArrayResolution(512, minArraySize));
        }

        public TextureArraySlot SetTexture(Material material, Texture2D texture)
        {
            //We only support square textures
            int resolution = texture.width;

            if (!resolutionDictionary.ContainsKey(resolution))
                resolutionDictionary.Add(resolution, new TextureArrayResolution(resolution, minArraySize));

            TextureArraySlot slot = resolutionDictionary[resolution].GetNextFreeSlot();
            Graphics.CopyTexture(texture, srcElement: 0, srcMip: 0, slot.TextureArray, dstElement: slot.UsedSlotIndex, dstMip: 0);
            material.SetInteger(arrayID, slot.UsedSlotIndex);
            material.SetTexture(textureID, slot.TextureArray);
            return slot;
        }
    }
}
