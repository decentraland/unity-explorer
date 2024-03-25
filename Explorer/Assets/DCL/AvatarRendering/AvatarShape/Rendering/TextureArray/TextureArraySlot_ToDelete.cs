using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public struct TextureArraySlot_ToDelete
    {
        public int UsedSlotIndex;
        public Texture2DArray TextureArray;
        public TextureArrayResolution_ToDelete TextureArrayResolution;

        public TextureArraySlot_ToDelete(int usedSlotIndex, Texture2DArray textureArray, TextureArrayResolution_ToDelete textureArrayResolution)
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