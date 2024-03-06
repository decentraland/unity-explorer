using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public struct TextureArraySlot
    {
        public int UsedSlotIndex;
        public Texture2DArray TextureArray;
        public TextureArraySlotHandler TextureArraySlotHandler;

        public TextureArraySlot(int usedSlotIndex, Texture2DArray textureArray, TextureArraySlotHandler textureArraySlotHandler)
        {
            UsedSlotIndex = usedSlotIndex;
            TextureArray = textureArray;
            TextureArraySlotHandler = textureArraySlotHandler;
        }

        public void FreeSlot()
        {
            TextureArraySlotHandler.FreeSlot(this);
        }
    }
}
