using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Places a loading intention that can be consumed by other systems in the pipeline.
    ///     Does not provide support for Video Textures
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [ThrottlingEnabled]
    public partial class StartMaterialsLoadingSystem : BaseUnityLoopSystem
    {
        private readonly DestroyMaterial destroyMaterial;
        private readonly ISceneData sceneData;
        private readonly int attemptsCount;
        private readonly IPerformanceBudget capFrameTimeBudget;

        public StartMaterialsLoadingSystem(World world, DestroyMaterial destroyMaterial, ISceneData sceneData, int attemptsCount, IPerformanceBudget capFrameTimeBudget) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
            this.sceneData = sceneData;
            this.attemptsCount = attemptsCount;
            this.capFrameTimeBudget = capFrameTimeBudget;
        }

        protected override void Update(float t)
        {
            InvalidateMaterialComponentQuery(World);
            CreateMaterialComponentQuery(World);
        }

        [Query]
        private void InvalidateMaterialComponent(ref PBMaterial material, ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            if (!material.IsDirty)
                return;

            material.IsDirty = false;

            MaterialData materialData = MaterialData.CreateFromPBMaterial(material, sceneData);

            if (MaterialDataEqualityComparer.INSTANCE.Equals(materialComponent.Data, materialData))
                return;

            // If isPbr is the same right the same material is reused
            if (materialComponent.Data.IsPbrMaterial != materialData.IsPbrMaterial)
            {
                ReleaseMaterial.Execute(World, ref materialComponent, destroyMaterial);
                materialComponent.Result = null;
            }

            materialComponent.Data = materialData;
            CreateGetTexturePromises(ref materialComponent, ref partitionComponent);
            materialComponent.Status = MaterialComponent.LifeCycle.LoadingInProgress;
        }

        [Query]
        [All(typeof(PBMaterial))]
        [None(typeof(MaterialComponent))]
        private void CreateMaterialComponent(in Entity entity, ref PBMaterial material, ref PartitionComponent partitionComponent)
        {
            if (!capFrameTimeBudget.TrySpendBudget())
                return;

            var materialComponent = new MaterialComponent(MaterialData.CreateFromPBMaterial(material, sceneData));
            StartLoad(ref materialComponent, ref partitionComponent);
            World.Add(entity, materialComponent);
        }

        private void StartLoad(ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            CreateGetTexturePromises(ref materialComponent, ref partitionComponent);
            materialComponent.Status = MaterialComponent.LifeCycle.LoadingInProgress;
        }

        private void CreateGetTexturePromises(ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            TryCreateGetTexturePromise(in materialComponent.Data.AlbedoTexture, ref materialComponent.AlbedoTexPromise, ref partitionComponent);

            if (materialComponent.Data.IsPbrMaterial)
            {
                TryCreateGetTexturePromise(in materialComponent.Data.AlphaTexture, ref materialComponent.AlphaTexPromise, ref partitionComponent);
                TryCreateGetTexturePromise(in materialComponent.Data.EmissiveTexture, ref materialComponent.EmissiveTexPromise, ref partitionComponent);
                TryCreateGetTexturePromise(in materialComponent.Data.BumpTexture, ref materialComponent.BumpTexPromise, ref partitionComponent);
            }
        }

        private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise, ref PartitionComponent partitionComponent)
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

            // If component is being reused forget the previous promise
            ReleaseMaterial.TryAddAbortIntention(World, ref promise);

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: attemptsCount),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);

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
    }
}
