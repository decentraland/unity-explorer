using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayType_ToDelete
    {
        internal readonly Dictionary<int, TextureArrayResolution_ToDelete> resolutionDictionary;
        private readonly int minArraySize;
        private readonly int arrayID;
        private readonly int textureID;
        private readonly TextureFormat textureFormat;

        public TextureArrayType_ToDelete(int minArraySize, int arrayID, int textureID, TextureFormat textureFormat)
        {
            resolutionDictionary = new Dictionary<int, TextureArrayResolution_ToDelete>();
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.textureFormat = textureFormat;

            //We initialize some default values
            resolutionDictionary.Add(256, new TextureArrayResolution_ToDelete(256, minArraySize, textureFormat));
            resolutionDictionary.Add(512, new TextureArrayResolution_ToDelete(512, minArraySize, textureFormat));
        }

        public TextureArraySlot_ToDelete SetTexture(Material material, Texture2D texture)
        {
            //We only support square textures
            int resolution = texture.width;

            if (!resolutionDictionary.ContainsKey(resolution))
                resolutionDictionary.Add(resolution, new TextureArrayResolution_ToDelete(resolution, minArraySize, textureFormat));

            var slot = resolutionDictionary[resolution].GetNextFreeSlot();
            Graphics.CopyTexture(texture, srcElement: 0, srcMip: 0, slot.TextureArray, dstElement: slot.UsedSlotIndex, dstMip: 0);
            material.SetInteger(arrayID, slot.UsedSlotIndex);
            material.SetTexture(textureID, slot.TextureArray);
            return slot;
        }
    }
}