using DCL.Optimization.Pools;
using System;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.VideoPlayer
{
    public static class VideoTextureFactory
    {
        public static ExtendedObjectPool<Texture2D> CreateVideoTexturesPool(int defaultCapacity = 5, int maxSize = 100) =>
            new (CreateVideoTexture, actionOnDestroy: UnityObjectUtils.SafeDestroy, actionOnRelease: CleanTexture, defaultCapacity: defaultCapacity, maxSize: maxSize);

        private static readonly Action<Texture2D> CleanTexture = texture2D =>
        {
            Debug.Log(Environment.StackTrace);

            // This allows to clear the existing data on the texture,
            // to avoid "ghost" images in the textures before they are loaded with new data,
            // particularly when dealing with streaming textures from videos
            texture2D.Reinitialize(1, 1);
            texture2D.SetPixel(0, 0, Color.clear);
            texture2D.Apply();
        };

        private static Texture2D CreateVideoTexture() =>
            new (1, 1, TextureFormat.BGRA32, false, false);
    }
}
