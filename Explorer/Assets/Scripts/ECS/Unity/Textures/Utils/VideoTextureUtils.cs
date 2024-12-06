using Arch.Core;
using CRDT;
using ECS.StreamableLoading.Textures;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Textures.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Textures.Utils
{
    public static class VideoTextureUtils
    {
        public static bool TryAddConsumer(
            this in TextureComponent textureComponent,
            Entity entity,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            IObjectPool<Texture2D> videoTexturesPool,
            World world,
            out Texture2DData? texture)
        {
            if (entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out Entity videoPlayerEntity) && world.IsAlive(videoPlayerEntity))
            {
                ref VideoTextureConsumer consumer = ref world.TryGetRef<VideoTextureConsumer>(videoPlayerEntity, out bool exists);

                if (!exists)
                {
                    world.Add(videoPlayerEntity, new VideoTextureConsumer(videoTexturesPool.Get()));
                    consumer = ref world.Get<VideoTextureConsumer>(videoPlayerEntity);
                }

                texture = consumer.Texture;
                texture.AddReference();

                ref PrimitiveMeshRendererComponent meshRenderer = ref world.TryGetRef<PrimitiveMeshRendererComponent>(entity, out bool hasMesh);

                if (hasMesh)
                    consumer.AddConsumerMeshRenderer(meshRenderer.MeshRenderer);

                return true;
            }

            texture = null;
            return false;
        }
    }
}
