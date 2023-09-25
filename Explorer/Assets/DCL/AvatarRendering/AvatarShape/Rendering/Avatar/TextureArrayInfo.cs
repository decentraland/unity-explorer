using DCL.AvatarRendering.AvatarShape.ComputeShader;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.Avatar
{
    public class TextureArrayInfo
    {
        private int nextFreeIndex;
        private readonly Texture2DArray[] arrays;
        private readonly int minArraySize;
        private readonly int resolution;

        private readonly Stack<int> freeSlots;

        public TextureArrayInfo(int resolution, int minArraySize)
        {
            this.minArraySize = minArraySize;
            this.resolution = resolution;

            arrays = new Texture2DArray[100];
            arrays[0] = CreateTexture2DArray();
            nextFreeIndex = 0;
            freeSlots = new Stack<int>();
        }

        public void AddAFreeSlot(int index) =>
            freeSlots.Push(index);

        public UsedTextureArraySlot GetNextFreeSlot(ComputeShaderHelpers.TextureArrayType type, int resolution)
        {
            if (freeSlots.TryPop(out int freeSlot))
            {
                int arrayIndex = freeSlot / minArraySize;

                if (arrays[arrayIndex] == null)
                    arrays[arrayIndex] = CreateTexture2DArray();

                return new UsedTextureArraySlot(type, resolution, freeSlot - (minArraySize * arrayIndex), arrayIndex, arrays[arrayIndex]);
            }
            else
            {
                int arrayIndex = nextFreeIndex / minArraySize;
                int slotIndex = nextFreeIndex - (minArraySize * arrayIndex);

                if (arrays[arrayIndex] == null)
                    arrays[arrayIndex] = CreateTexture2DArray();

                nextFreeIndex++;
                return new UsedTextureArraySlot(type, resolution, slotIndex, arrayIndex, arrays[arrayIndex]);
            }
        }

        private Texture2DArray CreateTexture2DArray()
        {
            var texture2DArray = new Texture2DArray(resolution, resolution, minArraySize, TextureFormat.BC7, false, false);
            texture2DArray.filterMode = FilterMode.Bilinear;
            texture2DArray.wrapMode = TextureWrapMode.Repeat;
            return texture2DArray;
        }

        public void FreeSlot(UsedTextureArraySlot usedTextureArraySlot)
        {
            freeSlots.Push((usedTextureArraySlot.usedArrayIndex * minArraySize) + usedTextureArraySlot.usedSlotIndex);
        }
    }

    public struct UsedTextureArraySlot
    {
        public ComputeShaderHelpers.TextureArrayType TextureArrayType;
        public int usedSlotIndex;
        public int usedArrayIndex;
        public Texture2DArray textureArray;
        public int resolution;

        public UsedTextureArraySlot(ComputeShaderHelpers.TextureArrayType textureArrayType, int resolution, int usedSlotIndex, int usedArrayIndex, Texture2DArray textureArray)
        {
            TextureArrayType = textureArrayType;
            this.usedSlotIndex = usedSlotIndex;
            this.usedArrayIndex = usedArrayIndex;
            this.textureArray = textureArray;
            this.resolution = resolution;
        }
    }
}
