using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Systems
{
    public abstract class CreateMaterialSystemBase : BaseUnityLoopSystem
    {
        private readonly int attemptsCount;
        private readonly IObjectPool<Material> materialsPool;

        protected CreateMaterialSystemBase(World world, IObjectPool<Material> materialsPool, int attemptsCount) : base(world)
        {
            this.attemptsCount = attemptsCount;
            this.materialsPool = materialsPool;
        }

        protected Material CreateNewMaterialInstance() =>
            materialsPool.Get();

        protected bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise promise)
        {
            if (textureComponent == null) return false;

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponent.Value.Src, attempts: attemptsCount),
                WrapMode = textureComponent.Value.WrapMode,
                FilterMode = textureComponent.Value.FilterMode,
            });

            return true;
        }

        protected void DestroyEntityReference(ref Promise promise)
        {
            promise.Consume(World);
        }

        protected bool TryGetTextureResult(ref Promise promise, out StreamableLoadingResult<Texture2D> textureResult)
        {
            textureResult = default(StreamableLoadingResult<Texture2D>);

            if (promise == Promise.NULL)
                return true;

            return promise.TryGetResult(World, out textureResult);
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId)
        {
            if (textureResult.Succeeded)
                material.SetTexture(propId, textureResult.Asset);
        }
    }
}
