using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayHandler
    {
        internal readonly TextureArraySlotHandler slotHandler;
        internal readonly int arrayID;
        internal readonly int textureID;
        internal TextureArraySlot defaultSlot;

        public TextureArrayHandler(int minArraySize, int arrayID, int textureID, int nResolution, TextureFormat textureFormat, Texture defaultTexture, int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT)
        {
            slotHandler = new TextureArraySlotHandler(nResolution, minArraySize, initialCapacityForEachResolution, textureFormat);
            this.arrayID = arrayID;
            this.textureID = textureID;
            InitalizeDefaultTexture(defaultTexture);
        }

        public TextureArrayHandler(int minArraySize, int arrayID, int textureID, int nResolution, TextureFormat textureFormat, int initialCapacityForEachResolution = PoolConstants.AVATARS_COUNT)
        {
            slotHandler = new TextureArraySlotHandler(nResolution, minArraySize, initialCapacityForEachResolution, textureFormat);
            this.arrayID = arrayID;
            this.textureID = textureID;
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
