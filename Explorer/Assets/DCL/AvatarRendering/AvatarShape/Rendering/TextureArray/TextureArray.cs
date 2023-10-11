using DCL.AvatarRendering.AvatarShape.ComputeShader;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArray
    {
        private readonly Dictionary<int, TextureArrayInfo> resolutionDictionary;
        private readonly int minArraySize;
        private readonly int arrayID;
        private readonly int textureID;

        public TextureArray(int minArraySize, int arrayID, int textureID)
        {
            resolutionDictionary = new Dictionary<int, TextureArrayInfo>();
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;

            //We initialize some default values
            resolutionDictionary.Add(256, new TextureArrayInfo(256, minArraySize));
            resolutionDictionary.Add(512, new TextureArrayInfo(512, minArraySize));
        }

        public UsedTextureArraySlot SetTexture(ComputeShaderConstants.TextureArrayType type, Material material, Texture2D texture)
        {
            //TODO: We are screwed with non squared textures
            int resolution = texture.width;

            if (!resolutionDictionary.ContainsKey(resolution))
                resolutionDictionary.Add(resolution, new TextureArrayInfo(resolution, minArraySize));

            UsedTextureArraySlot usedSlot = resolutionDictionary[resolution].GetNextFreeSlot(type, texture.width);
            Graphics.CopyTexture(texture, srcElement: 0, srcMip: 0, usedSlot.textureArray, dstElement: usedSlot.usedSlotIndex, dstMip: 0);
            material.SetInteger(arrayID, usedSlot.usedSlotIndex);
            material.SetTexture(textureID, usedSlot.textureArray);
            return usedSlot;
        }

        public void FreeTexture(UsedTextureArraySlot usedTextureArraySlot)
        {
            resolutionDictionary[usedTextureArraySlot.resolution].FreeSlot(usedTextureArraySlot);
        }
    }
}
