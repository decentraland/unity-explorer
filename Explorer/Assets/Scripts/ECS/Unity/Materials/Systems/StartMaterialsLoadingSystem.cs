using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;
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
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly IExtendedObjectPool<Texture2D> videoTexturesPool;

        public StartMaterialsLoadingSystem(World world, DestroyMaterial destroyMaterial, ISceneData sceneData, int attemptsCount, IPerformanceBudget capFrameTimeBudget,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, IExtendedObjectPool<Texture2D> videoTexturesPool) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
            this.sceneData = sceneData;
            this.attemptsCount = attemptsCount;
            this.capFrameTimeBudget = capFrameTimeBudget;
            this.entitiesMap = entitiesMap;
            this.videoTexturesPool = videoTexturesPool;
        }

        protected override void Update(float t)
        {
            InvalidateMaterialComponentQuery(World);
            CreateMaterialComponentQuery(World);
        }

        [Query]
        private void InvalidateMaterialComponent(ref PBMaterial material, ref MaterialComponent materialComponent, ref PartitionComponent partitionComponent)
        {
            if (material.IsDirty == false)
                return;

            material.IsDirty = false;

            MaterialData materialData = CreateMaterialData(ref material);

            if (MaterialDataEqualityComparer.Equals(in materialComponent.Data, in materialData))
                return;

            InvalidatePrbInequality(ref materialComponent, ref materialData);
            StartNewMaterialLoad(ref materialComponent, ref materialData, ref partitionComponent);
        }

        private void InvalidatePrbInequality(ref MaterialComponent materialComponent, ref MaterialData materialData)
        {
            // If isPbr is the same right the same material is reused
            if (materialComponent.Data.IsPbrMaterial != materialData.IsPbrMaterial)
            {
                ReleaseMaterial.Execute(World!, ref materialComponent, destroyMaterial);
                materialComponent.Result = null;
            }
        }

        private void StartNewMaterialLoad(ref MaterialComponent materialComponent, ref MaterialData newMaterialData, ref PartitionComponent partitionComponent)
        {
            materialComponent.Data = newMaterialData;
            CreateGetTexturePromises(ref materialComponent, ref partitionComponent);
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;
        }

        [Query]
        [All(typeof(PBMaterial))]
        [None(typeof(MaterialComponent))]
        private void CreateMaterialComponent(in Entity entity, ref PBMaterial material, ref PartitionComponent partitionComponent)
        {
            if (!capFrameTimeBudget.TrySpendBudget())
                return;

            var materialComponent = new MaterialComponent(CreateMaterialData(ref material));
            CreateGetTexturePromises(ref materialComponent, ref partitionComponent);
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            World.Add(entity, materialComponent);
        }

        private MaterialData CreateMaterialData(ref PBMaterial material)
        {
            if (material.Unlit != null)
                return CreateBasicMaterialData(material, albedoTexture: material.Unlit.Texture.CreateTextureComponent(sceneData));

            TextureComponent? albedoTexture = material.Pbr.Texture.CreateTextureComponent(sceneData);
            TextureComponent? alphaTexture = material.Pbr.AlphaTexture.CreateTextureComponent(sceneData);
            TextureComponent? emissiveTexture = material.Pbr.EmissiveTexture.CreateTextureComponent(sceneData);
            TextureComponent? bumpTexture = material.Pbr.BumpTexture.CreateTextureComponent(sceneData);

            return CreatePBRMaterialData(material, albedoTexture, alphaTexture, emissiveTexture, bumpTexture);
        }

        private static MaterialData CreatePBRMaterialData(
            in PBMaterial pbMaterial,
            in TextureComponent? albedoTexture,
            in TextureComponent? alphaTexture,
            in TextureComponent? emissiveTexture,
            in TextureComponent? bumpTexture) =>
            MaterialData.CreatePBRMaterial(
                albedoTexture,
                alphaTexture,
                emissiveTexture,
                bumpTexture,
                pbMaterial.GetAlphaTest(),
                pbMaterial.GetCastShadows(),
                pbMaterial.GetAlbedoColor(),
                pbMaterial.GetEmissiveColor(),
                pbMaterial.GetReflectiveColor(),
                pbMaterial.GetTransparencyMode(),
                pbMaterial.GetMetallic(),
                pbMaterial.GetRoughness(),
                pbMaterial.GetSpecularIntensity(),
                pbMaterial.GetEmissiveIntensity(),
                pbMaterial.GetDirectIntensity());

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

        private static MaterialData CreateBasicMaterialData(in PBMaterial pbMaterial, in TextureComponent? albedoTexture) =>
            MaterialData.CreateBasicMaterial(albedoTexture, pbMaterial.GetAlphaTest(), pbMaterial.GetDiffuseColor(), pbMaterial.GetCastShadows());

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

            var intention = new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: attemptsCount),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            };

            promise = textureComponent.Value.IsVideoTexture
                ? Promise.CreateFinalized(intention, GetOrAddVideoTextureResult(textureComponentValue))
                : Promise.Create(World!, intention, partitionComponent);

            return true;
        }

        private StreamableLoadingResult<Texture2D>? GetOrAddVideoTextureResult(TextureComponent textureComponent)
        {
            if (entitiesMap.TryGetValue(textureComponent.VideoPlayerEntity, out Entity videoPlayerEntity) && World.IsAlive(videoPlayerEntity))
            {
                if (World.Has<VideoTextureComponent>(videoPlayerEntity))
                    return new StreamableLoadingResult<Texture2D>(World.Get<VideoTextureComponent>(videoPlayerEntity).Texture);

                var videoTextureComponent = new VideoTextureComponent(videoTexturesPool.Get());
                World.Add(videoPlayerEntity, videoTextureComponent);
                return new StreamableLoadingResult<Texture2D>(videoTextureComponent.Texture);
            }

            var ecsSystemException = CreateException(new EcsEntityNotFoundException(textureComponent.VideoPlayerEntity, $"Entity {textureComponent.VideoPlayerEntity} not found!. VideoTexture will not be created."));
            return new StreamableLoadingResult<Texture2D>(ecsSystemException);
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
