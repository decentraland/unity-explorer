using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public struct TextureArraySlot
    {
        public int UsedSlotIndex;
        public Texture2DArray TextureArray;
        public TextureArrayResolution TextureArrayResolution;

        public TextureArraySlot(int usedSlotIndex, Texture2DArray textureArray, TextureArrayResolution textureArrayResolution)
        {
            UsedSlotIndex = usedSlotIndex;
            TextureArray = textureArray;
            TextureArrayResolution = textureArrayResolution;
        }

        public void FreeSlot()
        {
            TextureArrayResolution.FreeSlot(this);
        }
    }
}
