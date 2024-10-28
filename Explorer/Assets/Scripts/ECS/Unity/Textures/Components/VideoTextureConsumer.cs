using ECS.StreamableLoading.Textures;
using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public Texture2DData Texture { get; private set; }

        public int ConsumersCount => Texture.referenceCount;

        public VideoTextureConsumer(Texture2D texture)
        {
            Texture = new Texture2DData(texture);
        }

        public void Dispose()
        {
            // On Dispose video textures are dereferenced by material that acquired it
            Texture = null!;
        }
    }
}
