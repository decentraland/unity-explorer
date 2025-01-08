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
                    var texture2D = videoTexturesPool.Get();
                    //This allows to clear the existing data on the texture,
                    //to avoid "ghost" images in the textures before they are loaded with new data,
                    //particularly when dealing with streaming textures from videos
                    texture2D.Reinitialize(1, 1);
                    texture2D.SetPixel(0,0, Color.clear);
                    texture2D.Apply();
                    world.Add(videoPlayerEntity, new VideoTextureConsumer(texture2D));
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
