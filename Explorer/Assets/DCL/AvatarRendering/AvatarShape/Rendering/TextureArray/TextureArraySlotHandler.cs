using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public class TextureArraySlotHandler
    {
        internal readonly List<Texture2DArray> arrays;
        internal readonly Stack<TextureArraySlot> freeSlots;
        private readonly int minArraySize;
        private readonly int resolution;
        private int nextFreeIndex;
        private TextureFormat textureFormat;

        public TextureArraySlotHandler(int resolution, int minArraySize, int initialCapacity, TextureFormat _textureFormat)
        {
            this.minArraySize = minArraySize;
            this.resolution = resolution;
            this.textureFormat = _textureFormat;

            //Initial capacity for (100 * minArraySize) textures
            arrays = new List<Texture2DArray>(initialCapacity);
            arrays.Add(CreateTexture2DArray());
            freeSlots = new Stack<TextureArraySlot>();
        }

        public TextureArraySlot GetNextFreeSlot()
        {
            if (freeSlots.TryPop(out TextureArraySlot freeSlot)) { return freeSlot; }

            int arrayIndex = nextFreeIndex / minArraySize;
            int slotIndex = nextFreeIndex - (minArraySize * arrayIndex);

            if (arrays.Count <= arrayIndex)
                arrays.Add(CreateTexture2DArray());

            nextFreeIndex++;
            return new TextureArraySlot(slotIndex, arrays[arrayIndex], this);
        }

        private Texture2DArray CreateTexture2DArray()
        {
            var texture2DArray = new Texture2DArray(resolution, resolution, minArraySize, textureFormat, false, false);
            texture2DArray.filterMode = FilterMode.Bilinear;
            texture2DArray.wrapMode = TextureWrapMode.Repeat;
            texture2DArray.anisoLevel = 9;
            return texture2DArray;
        }

        public void FreeSlot(TextureArraySlot textureArraySlot)
        {
            freeSlots.Push(textureArraySlot);
        }
        
    }
}
