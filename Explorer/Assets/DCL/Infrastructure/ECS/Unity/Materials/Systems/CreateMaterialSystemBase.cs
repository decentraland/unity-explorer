using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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

        protected bool TryGetTextureResult(ref Promise? promise, out StreamableLoadingResult<Texture2DData> textureResult)
        {
            textureResult = default(StreamableLoadingResult<Texture2DData>);

            if (promise == null)
                return true;

            Promise value = promise.Value;
            bool result = value.TryGetResult(World, out textureResult);

            // Write the value back as `promise.Value` produces a copy of the struct
            promise = value;
            return result;
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2DData> textureResult, int propId, in TextureComponent? textureComponent)
        {
            if (!textureResult.Succeeded) return;

            if (textureResult.Asset!.hack != null)
                material.SetTexture(propId, textureResult.Asset!.hack);
            else
                material.SetTexture(propId, textureResult.Asset!);

            material.SetTextureScale(propId, textureComponent.Value.TextureTiling);
            material.SetTextureOffset(propId, textureComponent.Value.TextureOffset);

            //TODO (Materials should be cleaned on a pool)
        }
    }
}
