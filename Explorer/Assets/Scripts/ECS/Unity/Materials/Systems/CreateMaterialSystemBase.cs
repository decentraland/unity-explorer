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

        protected bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise)
        {
            if (textureComponent == null)
            {
                // If component is being reuse forget the previous promise
                ReleaseMaterial.TryAddAbortIntention(World, ref promise);
                return false;
            }

            TextureComponent textureComponentValue = textureComponent.Value;

            // If data inside promise has not changed just reuse the same promise
            // as creating and waiting for a new one can be expensive
            if (Equals(ref textureComponentValue, ref promise))
                return false;

            // If component is being reuse forget the previous promise
            ReleaseMaterial.TryAddAbortIntention(World, ref promise);

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: attemptsCount),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            });

            return true;
        }

        private static bool Equals(ref TextureComponent textureComponent, ref Promise? promise)
        {
            if (promise == null) return false;

            Promise promiseValue = promise.Value;

            GetTextureIntention intention = promiseValue.LoadingIntention;

            return textureComponent.Src == promiseValue.LoadingIntention.CommonArguments.URL &&
                   textureComponent.WrapMode == intention.WrapMode &&
                   textureComponent.FilterMode == intention.FilterMode;
        }

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
