using System;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    [Serializable]
    public struct TextureArrayResolutionDescriptor
    {
        public int Resolution;
        public int ArraySize;
        public int InitialArrayCapacity;

        public TextureArrayResolutionDescriptor(int resolution, int arraySize, int initialArrayCapacity)
        {
            Resolution = resolution;
            ArraySize = arraySize;
            InitialArrayCapacity = initialArrayCapacity;
        }
        
    }
}