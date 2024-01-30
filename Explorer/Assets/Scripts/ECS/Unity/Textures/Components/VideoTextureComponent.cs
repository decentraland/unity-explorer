using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureComponent : IDisposable
    {
        public Texture2D Texture { get; private set; }

        public VideoTextureComponent(Texture2D texture)
        {
            Texture = texture;
        }

        public void Dispose()
        {
            Texture = null!;
        }
    }
}
