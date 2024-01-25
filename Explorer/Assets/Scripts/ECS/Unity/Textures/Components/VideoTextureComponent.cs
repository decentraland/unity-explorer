using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureComponent : IDisposable
    {
        public Texture2D Texture { get; private set; }
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;

        public VideoTextureComponent(IExtendedObjectPool<Texture2D> videoTexturePool)
        {
            this.videoTexturePool = videoTexturePool;
            Texture = this.videoTexturePool.Get();
        }

        public void Dispose()
        {
            videoTexturePool.Release(Texture);
            Texture = null;
        }
    }
}
