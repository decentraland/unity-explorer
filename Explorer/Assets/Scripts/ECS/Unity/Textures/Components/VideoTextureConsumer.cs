using Arch.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public Texture2D Texture { get; private set; }

        private HashSet<Entity>? consumers;

        public int ConsumersCount => consumers!.Count;

        public VideoTextureConsumer(Texture2D texture)
        {
            Texture = texture;
            consumers = HashSetPool<Entity>.Get();
        }

        public void AddConsumer(Entity entity)
        {
            consumers!.Add(entity);
        }

        public void RemoveConsumer(Entity entity)
        {
            consumers!.Remove(entity);
        }

        public void Dispose()
        {
            Texture = null!;
            HashSetPool<Entity>.Release(consumers);
            consumers = null;
        }
    }
}
