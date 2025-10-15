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
            World world,
            out Texture? texture)
        {
            texture = null;

            if (!entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out var videoPlayerEntity) || !world.IsAlive(videoPlayerEntity))
                return false;

            ref var consumer = ref world.TryGetRef<VideoTextureConsumer>(videoPlayerEntity, out bool hasConsumer);

            if (!hasConsumer)
                return false;

            if (world.TryGet(entity, out PrimitiveMeshRendererComponent primitiveMeshComponent))
            {
                consumer.AddConsumer(primitiveMeshComponent.MeshRenderer);
            }
            else if (world.TryGet(entity, out GltfNode gltfNode))
            {
                foreach (var renderer in gltfNode.Renderers)
                    consumer.AddConsumer(renderer);
            }

            consumer.AddReference();
            texture = consumer.Texture;
            return true;
        }

        public struct VideoRenderingInfo
        {
            public Entity VideoPlayer;

            public Texture2DData? VideoTexture;

            public Renderer? VideoRenderer;
        }
    }
}
