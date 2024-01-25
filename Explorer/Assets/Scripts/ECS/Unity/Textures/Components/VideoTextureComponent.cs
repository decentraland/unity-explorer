using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public readonly struct VideoTextureComponent : IDisposable
    {
        public readonly Texture2D Texture;

        private readonly IExtendedObjectPool<Texture2D> texturesPool;

        public VideoTextureComponent(IExtendedObjectPool<Texture2D> texturesPool)
        {
            this.texturesPool = texturesPool;
            Texture = texturesPool.Get();
        }

        public void Dispose()
        {
            texturesPool.Release(Texture);
        }
    }
}
