using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Systems
{
    public abstract class CreateMaterialSystemBase : BaseUnityLoopSystem
    {
        private readonly IObjectPool<Material> materialsPool;

        protected CreateMaterialSystemBase(World world, IObjectPool<Material> materialsPool) : base(world)
        {
            this.materialsPool = materialsPool;
        }

        protected Material CreateNewMaterialInstance() =>
            materialsPool.Get();

        protected void DestroyEntityReference(ref Promise? promise)
        {
            promise?.Consume(World);
        }

        protected bool TryGetTextureResult(ref Promise? promise, out StreamableLoadingResult<Texture2D> textureResult)
        {
            textureResult = default(StreamableLoadingResult<Texture2D>);

            if (promise == null)
                return true;

            return promise.Value.TryGetResult(World, out textureResult);
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId)
        {
            if (textureResult.Succeeded)
                material.SetTexture(propId, textureResult.Asset);
        }
    }
}
