using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.Unity.Textures.Components;
using UnityEngine;
using UnityEngine.Assertions;

namespace ECS.Unity.Materials.Systems
{
    public abstract class CreateMaterialSystemBase : BaseUnityLoopSystem
    {
        private readonly int attemptsCount;
        protected readonly IMaterialsCache materialsCache;

        internal Material sharedMaterial { get; private set; }

        protected CreateMaterialSystemBase(World world, IMaterialsCache materialsCache, int attemptsCount) : base(world)
        {
            this.materialsCache = materialsCache;
            this.attemptsCount = attemptsCount;
        }

        internal abstract string materialPath { get; }

        public override void Initialize()
        {
            sharedMaterial = Resources.Load<Material>(materialPath);
            Assert.IsNotNull(sharedMaterial);
        }

        protected Material CreateNewMaterialInstance() =>
            new (sharedMaterial);

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
            if (!entityReference.IsAlive(World))
                return;

            World.Destroy(entityReference);
        }

        protected bool TryGetTextureResult(in EntityReference entityReference, out StreamableLoadingResult<Texture2D> textureResult)
        {
            if (!entityReference.IsAlive(World))
            {
                textureResult = default(StreamableLoadingResult<Texture2D>);
                return false;
            }

            if (!World.TryGet(entityReference, out textureResult))
            {
                textureResult = default(StreamableLoadingResult<Texture2D>);
                return false;
            }

            return true;
        }

        protected static void TrySetTexture(Material material, ref StreamableLoadingResult<Texture2D> textureResult, int propId)
        {
            if (textureResult.Succeeded)
                material.SetTexture(propId, textureResult.Asset);
        }
    }
}
