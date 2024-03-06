using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayHandler
    {
        internal readonly TextureArraySlotHandler slotHandler;
        private readonly int initialCapacityForEachResolution;
        private readonly int minArraySize;
        private readonly int arrayID;
        private readonly int textureID;
        private TextureArraySlot defaultSlot;

        public TextureArrayHandler(int minArraySize, int arrayID, int textureID, int _nResolution, TextureFormat _textureFormat, Texture defaultTexture, int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT)
        {
            slotHandler = new TextureArraySlotHandler(_nResolution, minArraySize, initialCapacityForEachResolution, _textureFormat);
            this.minArraySize = minArraySize;
            this.arrayID = arrayID;
            this.textureID = textureID;
            this.initialCapacityForEachResolution = initialCapacityForEachResolution;
            InitalizeDefaultTexture(defaultTexture);
        }

        private void InitalizeDefaultTexture(Texture defaultTexture)
        {
            defaultSlot = slotHandler.GetNextFreeSlot();
            Graphics.CopyTexture(defaultTexture, srcElement: 0, srcMip: 0, defaultSlot.TextureArray, dstElement: defaultSlot.UsedSlotIndex, dstMip: 0);
        }

        public TextureArraySlot SetTexture(Material material, Texture2D texture)
        {
            TextureArraySlot slot = slotHandler.GetNextFreeSlot();
            int mipLevel = 0;
            //for (int mipLevel = 0; mipLevel < texture.mipmapCount; ++mipLevel)
            {
                Graphics.CopyTexture(texture, srcElement: 0, srcMip: mipLevel, slot.TextureArray, dstElement: slot.UsedSlotIndex, dstMip: mipLevel);
            }
            material.SetInteger(arrayID, slot.UsedSlotIndex);
            material.SetTexture(textureID, slot.TextureArray);
            return slot;
        }
        
        public void SetDefaultTexture(Material material)
        {
            material.SetInteger(arrayID, defaultSlot.UsedSlotIndex);
            material.SetTexture(textureID, defaultSlot.TextureArray);
        }
    }
}
