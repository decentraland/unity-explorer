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
            out VideoRenderingInfo info)
        {
            info = default(VideoRenderingInfo);

            if (!entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out info.VideoPlayer) || !world.IsAlive(info.VideoPlayer))
                return false;

            ref VideoTextureConsumer consumer = ref world.TryGetRef<VideoTextureConsumer>(info.VideoPlayer, out bool hasConsumer);

            if (!hasConsumer)
                consumer = ref CreateTextureConsumer(world, videoTexturesPool.Get(), info.VideoPlayer);

            info.VideoTexture = consumer.Texture;
            info.VideoTexture.AddReference();

            if (world.TryGet(entity, out PrimitiveMeshRendererComponent primitiveMeshComponent))
            {
                info.VideoRenderer = primitiveMeshComponent.MeshRenderer;
                consumer.AddConsumer(primitiveMeshComponent.MeshRenderer);
            }
            else if (world.TryGet(entity, out GltfNode gltfNode))
            {
                // Use the first renderer for VideoRenderer (for backward compatibility)
                info.VideoRenderer = gltfNode.Renderers.Count > 0 ? gltfNode.Renderers[0] : null;

                // Add all renderers as consumers
                foreach (var renderer in gltfNode.Renderers)
                    consumer.AddConsumer(renderer);
            }

            return true;
        }

        private static ref VideoTextureConsumer CreateTextureConsumer(World world, Texture2D texture, Entity videoPlayerEntity)
        {
            // This allows to clear the existing data on the texture,
            // to avoid "ghost" images in the textures before they are loaded with new data,
            // particularly when dealing with streaming textures from videos
            texture.Reinitialize(1, 1);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();

            world.Add(videoPlayerEntity, new VideoTextureConsumer(texture));
            return ref world.Get<VideoTextureConsumer>(videoPlayerEntity);
        }

        public struct VideoRenderingInfo
        {
            public Entity VideoPlayer;

            public Texture2DData? VideoTexture;

            public Renderer? VideoRenderer;
        }
    }
}
