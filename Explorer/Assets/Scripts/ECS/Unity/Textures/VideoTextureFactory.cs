using DCL.Optimization.Pools;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.VideoPlayer
{
    public static class VideoTextureFactory
    {
        public static ExtendedObjectPool<Texture2D> CreateVideoTexturesPool(int defaultCapacity = 5, int maxSize = 100) =>
            new (CreateVideoTexture, actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: defaultCapacity, maxSize: maxSize);

        private static Texture2D CreateVideoTexture() =>
            new (1, 1, TextureFormat.BGRA32, false, false);
    }
}
