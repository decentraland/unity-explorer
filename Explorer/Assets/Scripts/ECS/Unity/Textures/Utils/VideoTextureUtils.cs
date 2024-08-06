using Arch.Core;
using CRDT;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Textures.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Textures.Utils
{
    public static class VideoTextureUtils
    {
        public static bool TryAddConsumer(
            this in TextureComponent textureComponent,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            Entity consumerEntity,
            IObjectPool<Texture2D> videoTexturesPool,
            World world,
            out Texture2D? texture)
        {
            if (entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out Entity videoPlayerEntity) && world.IsAlive(videoPlayerEntity))
            {
                ref VideoTextureConsumer consumer = ref world.TryGetRef<VideoTextureConsumer>(videoPlayerEntity, out bool exists);

                if (!exists)
                {
                    world.Add(videoPlayerEntity, new VideoTextureConsumer(videoTexturesPool.Get()));
                    consumer = ref world.Get<VideoTextureConsumer>(videoPlayerEntity);
                }

                consumer.AddConsumer(consumerEntity);

                texture = consumer.Texture;
                return true;
            }

            texture = null;
            return false;
        }

        public static void RemoveConsumer(CRDTEntity videoPlayerSDKEntity, Entity consumerEntity, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, World world)
        {
            if (entitiesMap.TryGetValue(videoPlayerSDKEntity, out Entity videoPlayerEntity) && world.IsAlive(videoPlayerEntity))
            {
                ref VideoTextureConsumer consumer = ref world.TryGetRef<VideoTextureConsumer>(videoPlayerEntity, out bool exists);

                if (!exists) return;

                consumer.RemoveConsumer(consumerEntity);
            }
        }
    }
}
