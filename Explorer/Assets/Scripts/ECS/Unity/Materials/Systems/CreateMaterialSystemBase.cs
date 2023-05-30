using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Pool;

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

        protected bool TryCreateGetTextureIntention(in TextureComponent? textureComponent, ref EntityReference entityReference)
        {
            if (textureComponent == null) return false;

            Entity entity = World.Create(new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponent.Value.Src, attempts: attemptsCount),
                WrapMode = textureComponent.Value.WrapMode,
                FilterMode = textureComponent.Value.FilterMode,
            });

            entityReference = World.Reference(entity);
            return true;
        }

        protected void DestroyEntityReference(in EntityReference entityReference)
        {
            if (entityReference == EntityReference.Null || !entityReference.IsAlive(World))
                return;

            World.Destroy(entityReference);
        }

        protected bool TryGetTextureResult(in EntityReference entityReference, out StreamableLoadingResult<Texture2D> textureResult)
        {
            textureResult = default(StreamableLoadingResult<Texture2D>);

            if (entityReference == EntityReference.Null)
                return true;

            if (!entityReference.IsAlive(World))
                return false;

            return World.TryGet(entityReference, out textureResult);
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId)
        {
            if (textureResult.Succeeded)
                material.SetTexture(propId, textureResult.Asset);
        }
    }
}
