using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Materials.Components;
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

        protected void DestroyEntityReferencesForPromises(ref MaterialComponent materialComponent)
        {
            DestroyEntityReference(ref materialComponent.AlbedoTexPromise);
            DestroyEntityReference(ref materialComponent.EmissiveTexPromise);
            DestroyEntityReference(ref materialComponent.AlphaTexPromise);
            DestroyEntityReference(ref materialComponent.BumpTexPromise);
        }

        protected void DestroyEntityReference(ref Promise? promise)
        {
            if (promise == null) return;
            Promise promiseValue = promise.Value;
            promiseValue.Consume(World);

            // Write the value back as `promise.Value` produces a copy of the struct
            promise = promiseValue;
        }

        protected bool TryGetTextureResult(ref Promise? promise, out StreamableLoadingResult<Texture2D> textureResult)
        {
            textureResult = default(StreamableLoadingResult<Texture2D>);

            if (promise == null)
                return true;

            Promise value = promise.Value;
            bool result = value.TryGetResult(World, out textureResult);

            // Write the value back as `promise.Value` produces a copy of the struct
            promise = value;
            return result;
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId, in TextureComponent? textureComponent)
        {
            if (!textureResult.Succeeded) return;

            material.SetTexture(propId, textureResult.Asset);

            if (textureComponent is { IsVideoTexture: true })
                material.SetTextureScale(propId, VIDEO_TEXTURE_VERTICAL_FLIP);

            // When the material is re-used for another texture we need to restore the texture scale
            // Otherwise in case it was previously a video texture it gets x:1,y:-1 scale which is undesired
            // This case happens on nft-museum at sdk-goerli-plaza 85,-8. A plane exists which has a texture that acts as a "preview" of the stream.
            // Whenever you get close or far, the texture is changed either to video stream or regular exposing this issue
            else
                material.SetTextureScale(propId, Vector2.one);
        }
    }
}
