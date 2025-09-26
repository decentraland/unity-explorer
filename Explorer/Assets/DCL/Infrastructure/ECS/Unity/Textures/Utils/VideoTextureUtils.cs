using Arch.Core;
using CRDT;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GltfNodeModifiers.Components;
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
            string sceneId,
            out Texture2DData? texture)
        {
            texture = null;

            if (!entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out var videoPlayerEntity) || !world.IsAlive(videoPlayerEntity))
                return false;

            if (world.Has<VideoTextureConsumer>(videoPlayerEntity))
            {
                ref VideoTextureConsumer consumer = ref world.Get<VideoTextureConsumer>(videoPlayerEntity);
                Debug.Log($"JUANI REUSING VIDEO TEXTURE CONSUMER {videoPlayerEntity} {consumer.Texture.Asset.GetInstanceID()} {sceneId}");

                if (world.TryGet(entity, out PrimitiveMeshRendererComponent primitiveMeshComponent)) { consumer.AddConsumer(primitiveMeshComponent.MeshRenderer); }
                else if (world.TryGet(entity, out GltfNode gltfNode))
                {
                    foreach (Renderer? renderer in gltfNode.Renderers)
                        consumer.AddConsumer(renderer);
                }

                consumer.Texture.AddReference();
                consumer.IsDirty = true;

                texture = consumer.Texture;
            }
            else
            {
                Texture2D texturedGotten = videoTexturesPool.Get();

                var consumer = new VideoTextureConsumer(texturedGotten);
                Debug.Log($"JUANI GETTING NEW VIDEO TEXTURE CONSUMER {videoPlayerEntity} {texturedGotten.GetInstanceID()} {sceneId}");

                if (world.TryGet(entity, out PrimitiveMeshRendererComponent primitiveMeshComponent)) { consumer.AddConsumer(primitiveMeshComponent.MeshRenderer); }
                else if (world.TryGet(entity, out GltfNode gltfNode))
                {
                    foreach (Renderer? renderer in gltfNode.Renderers)
                        consumer.AddConsumer(renderer);
                }

                consumer.Texture.AddReference();
                consumer.IsDirty = true;

                texture = consumer.Texture;

                world.Add(entity, consumer);
            }

            return true;
        }
    }

}
