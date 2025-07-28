﻿using Arch.Core;
using ECS.StreamableLoading.Cache;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Textures.Components;
using UnityEngine;
using Utility.Primitives;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials
{
    public delegate void DestroyMaterial(in MaterialData materialData, Material material);

    /// <summary>
    ///     Executes the logic to clean-up material
    /// </summary>
    public static class ReleaseMaterial
    {
        public static void TryReleaseDefault(ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (!primitiveMeshRendererComponent.DefaultMaterialIsUsed) return;

            DefaultMaterial.Release(primitiveMeshRendererComponent.MeshRenderer.sharedMaterial);
            primitiveMeshRendererComponent.DefaultMaterialIsUsed = false;
        }

        public static void Execute(Entity entity, World world, ref MaterialComponent materialComponent, DestroyMaterial destroyMaterial)
        {
            switch (materialComponent.Status)
            {
                // Dereference the loaded material
                case StreamableLoading.LifeCycle.LoadingFinished or StreamableLoading.LifeCycle.Applied when materialComponent.Result:
                    ReleaseTextures(ref materialComponent, false);
                    destroyMaterial(in materialComponent.Data, materialComponent.Result);
                    break;

                // Abort the loading process of the textures
                case StreamableLoading.LifeCycle.LoadingInProgress:
                    ReleaseTextures(ref materialComponent, true);
                    break;
            }

            void ReleaseTextures(ref MaterialComponent mat, bool forgetLoading)
            {
                ReleaseIntention(entity, world, ref mat.AlbedoTexPromise, forgetLoading);
                ReleaseIntention(entity, world, ref mat.EmissiveTexPromise, forgetLoading);
                ReleaseIntention(entity, world, ref mat.AlphaTexPromise, forgetLoading);
                ReleaseIntention(entity, world, ref mat.BumpTexPromise, forgetLoading);
            }
        }

        internal static void ReleaseIntention(Entity entity, World world, ref Promise? promise, bool forgetLoading)
        {
            if (promise == null)
                return;

            Promise promiseValue = promise.Value;

            if (forgetLoading)
                promiseValue.ForgetLoading(world);

            if (promiseValue.LoadingIntention.IsVideoTexture)
            {
                ref VideoTextureConsumer consumer = ref world.TryGetRef<VideoTextureConsumer>(entity, out bool hasConsumer);

                if (hasConsumer)
                {
                    if (world.TryGet(entity, out PrimitiveMeshRendererComponent primitiveMeshComponent))
                        consumer.RemoveConsumer(primitiveMeshComponent.MeshRenderer);
                    else if (world.TryGet(entity, out GltfNode gltfNode))
                    {
                        foreach (var renderer in gltfNode.Renderers)
                            consumer.RemoveConsumer(renderer);
                    }
                }
            }

            promiseValue.TryDereference(world);

            // Nullify the entity reference
            promise = null;
        }
    }
}
