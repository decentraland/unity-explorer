using Arch.Core;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using UnityEngine;
using Utility.Primitives;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials
{
    public delegate void DestroyMaterial(in MaterialData materialData, Material material);

    /// <summary>
    ///     Executes the logic to clean-up material
    /// </summary>
    public static class ReleaseMaterial
    {
        public static void Execute(World world, ref MaterialComponent materialComponent, IMaterialsCache materialsCache)
        {
            switch (materialComponent.Status)
            {
                // Dereference the loaded material
                case StreamableLoading.LifeCycle.LoadingFinished or StreamableLoading.LifeCycle.Applied when materialComponent.Result:
                    materialsCache.Dereference(in materialComponent.Data);
                    break;

                // Abort the loading process of the textures
                case StreamableLoading.LifeCycle.LoadingInProgress:
                    TryAddAbortIntention(world, ref materialComponent.AlbedoTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.EmissiveTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.AlphaTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.BumpTexPromise);
                    break;
            }

            materialComponent.Status = StreamableLoading.LifeCycle.LoadingNotStarted;
        }

        public static void TryReleaseDefault(ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (!primitiveMeshRendererComponent.DefaultMaterialIsUsed) return;

            DefaultMaterial.Release(primitiveMeshRendererComponent.MeshRenderer.sharedMaterial);
            primitiveMeshRendererComponent.DefaultMaterialIsUsed = false;
        }

        public static void Execute(World world, ref MaterialComponent materialComponent, DestroyMaterial destroyMaterial)
        {
            switch (materialComponent.Status)
            {
                // Dereference the loaded material
                case StreamableLoading.LifeCycle.LoadingFinished or StreamableLoading.LifeCycle.Applied when materialComponent.Result:
                    destroyMaterial(in materialComponent.Data, materialComponent.Result);
                    break;

                // Abort the loading process of the textures
                case StreamableLoading.LifeCycle.LoadingInProgress:
                    TryAddAbortIntention(world, ref materialComponent.AlbedoTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.EmissiveTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.AlphaTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.BumpTexPromise);
                    break;
            }
        }

        internal static void TryAddAbortIntention(World world, ref Promise? promise)
        {
            if (promise == null)
                return;

            promise.Value.ForgetLoading(world);

            // Nullify the entity reference
            promise = null;
        }
    }
}
