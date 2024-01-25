using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public readonly struct VideoTextureComponent : IDisposable
    {
        public readonly Texture2D Texture;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;

        public VideoTextureComponent(IExtendedObjectPool<Texture2D> videoTexturePool)
        {
            this.videoTexturePool = videoTexturePool;
            Texture = this.videoTexturePool.Get();
        }

        public void Dispose()
        {
            videoTexturePool.Release(Texture);
        }
    }
}
