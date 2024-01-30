using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Systems
{
    public abstract class CreateMaterialSystemBase : BaseUnityLoopSystem
    {
        private static readonly Vector2 VIDEO_TEXTURE_VERTICAL_FLIP = new (1, -1);
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

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId, in TextureComponent? textureComponent)
        {
            if (!textureResult.Succeeded) return;

            material.SetTexture(propId, textureResult.Asset);

            if (textureComponent is { IsVideoTexture: true })
                material.SetTextureScale(propId, VIDEO_TEXTURE_VERTICAL_FLIP);
        }
    }
}
