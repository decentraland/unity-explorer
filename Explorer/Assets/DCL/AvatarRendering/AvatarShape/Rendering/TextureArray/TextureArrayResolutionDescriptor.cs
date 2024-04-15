using System;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    [Serializable]
    public struct TextureArrayResolutionDescriptor
    {
        public int Resolution;
        public int ArraySize;

        public TextureArrayResolutionDescriptor(int resolution, int arraySize)
        {
            Resolution = resolution;
            ArraySize = arraySize;
        }
    }
}