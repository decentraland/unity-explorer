using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArrayResolution_ToDelete
    {
        internal readonly List<Texture2DArray> arrays;

        internal readonly Stack<TextureArraySlot_ToDelete> freeSlots;
        private readonly int minArraySize;
        private readonly int resolution;
        private int nextFreeIndex;
        private readonly TextureFormat textureFormat;

        public TextureArrayResolution_ToDelete(int resolution, int minArraySize, TextureFormat textureFormat)
        {
            this.minArraySize = minArraySize;
            this.resolution = resolution;
            this.textureFormat = textureFormat;

            //Initial capacity for (100 * minArraySize) texutres
            arrays = new List<Texture2DArray>(100);
            arrays.Add(CreateTexture2DArray(textureFormat));
            nextFreeIndex = 0;
            freeSlots = new Stack<TextureArraySlot_ToDelete>();
        }

        public TextureArraySlot_ToDelete GetNextFreeSlot()
        {
            if (freeSlots.TryPop(out var freeSlot)) { return freeSlot; }

            int arrayIndex = nextFreeIndex / minArraySize;
            int slotIndex = nextFreeIndex - minArraySize * arrayIndex;

            if (arrays.Count <= arrayIndex)
                arrays.Add(CreateTexture2DArray(textureFormat));

            nextFreeIndex++;
            return new TextureArraySlot_ToDelete(slotIndex, arrays[arrayIndex], this);
        }

        private Texture2DArray CreateTexture2DArray(TextureFormat textureFormat)
        {
            var texture2DArray = new Texture2DArray(resolution, resolution, minArraySize, textureFormat, false, false);
            texture2DArray.filterMode = FilterMode.Bilinear;
            texture2DArray.wrapMode = TextureWrapMode.Repeat;
            texture2DArray.anisoLevel = 9;
            return texture2DArray;
        }

        public void FreeSlot(TextureArraySlot_ToDelete textureArraySlot)
        {
            freeSlots.Push(textureArraySlot);
        }
        
    }
}