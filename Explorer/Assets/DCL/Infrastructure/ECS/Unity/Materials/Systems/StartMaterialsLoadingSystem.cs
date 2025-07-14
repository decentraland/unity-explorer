using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.MediaStream;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using ECS.Unity.Textures.Utils;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
        private void InvalidateMaterialComponent(Entity entity, ref PBMaterial material, ref MaterialComponent materialComponent, PartitionComponent partitionComponent)
        {
            if (material.IsDirty == false)
                return;

            material.IsDirty = false;

            MaterialData materialData = CreateMaterialData(in material);

            if (MaterialDataEqualityComparer.Equals(in materialComponent.Data, in materialData))
                return;

            InvalidatePrbInequality(entity, ref materialComponent, ref materialData);

            MaterialData.TexturesData prevTextureData = materialComponent.Data.Textures;
            materialComponent.Data = materialData;
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            World.Set(entity, StartNewMaterialLoad(entity, materialComponent, in prevTextureData, partitionComponent));
            World.AddOrGet(entity, new ShouldInstanceMaterialComponent());
        }

        private void InvalidatePrbInequality(Entity entity, ref MaterialComponent materialComponent, ref MaterialData materialData)
        {
            // If isPbr is the same right the same material is reused
            if (materialComponent.Data.IsPbrMaterial != materialData.IsPbrMaterial)
            {
                ReleaseMaterial.Execute(entity, World!, ref materialComponent, destroyMaterial);
                materialComponent.Result = null;
            }
        }

        private MaterialComponent StartNewMaterialLoad(Entity entity, MaterialComponent materialComponent, in MaterialData.TexturesData prevTexturesData, PartitionComponent partitionComponent)
        {
            TryCreateGetTexturePromises(entity, ref materialComponent, prevTexturesData, partitionComponent);
            return materialComponent;
        }

        [Query]
        [All(typeof(PBMaterial))]
        [None(typeof(MaterialComponent))]
        private void CreateMaterialComponent(Entity entity, ref PBMaterial material, ref PartitionComponent partitionComponent)
        {
            if (!capFrameTimeBudget.TrySpendBudget())
                return;

            var materialComponent = new MaterialComponent(CreateMaterialData(in material));
            bool success = TryCreateGetTexturePromises(entity, ref materialComponent, null, partitionComponent);

            // If the needed texture promises creation was unsuccessful, let's try again in a next iteration
            // (e.g.: when dealing with a VideoTexture, the video entity may take extra frames to be registered...)
            if (!success)
            {
                materialComponent.AlbedoTexPromise?.ForgetLoading(World);
                materialComponent.AlphaTexPromise?.ForgetLoading(World);
                if (materialComponent.Data.IsPbrMaterial)
                {
                    materialComponent.EmissiveTexPromise?.ForgetLoading(World);
                    materialComponent.BumpTexPromise?.ForgetLoading(World);
                }

                return;
            }

            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            World.Add(entity, materialComponent, new ShouldInstanceMaterialComponent());
        }

        private MaterialData CreateMaterialData(in PBMaterial material)
        {
            if (material.Unlit != null)
                return CreateBasicMaterialData(material, albedoTexture: material.Unlit.Texture.CreateTextureComponent(sceneData), material.Unlit.AlphaTexture.CreateTextureComponent(sceneData));

            TextureComponent? albedoTexture = material.Pbr.Texture.CreateTextureComponent(sceneData);
            TextureComponent? alphaTexture = material.Pbr.AlphaTexture.CreateTextureComponent(sceneData);
            TextureComponent? emissiveTexture = material.Pbr.EmissiveTexture.CreateTextureComponent(sceneData);
            TextureComponent? bumpTexture = material.Pbr.BumpTexture.CreateTextureComponent(sceneData)?.WithTextureType(TextureType.NormalMap);

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

        private bool TryCreateGetTexturePromises(Entity entity, ref MaterialComponent materialComponent,
            in MaterialData.TexturesData? oldTexturesData,
            PartitionComponent partitionComponent)
        {
            bool success = false;

            success = TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.AlbedoTexture, oldTexturesData?.AlbedoTexture, ref materialComponent.AlbedoTexPromise, partitionComponent)
                      || !materialComponent.Data.Textures.AlbedoTexture.HasValue;

            success &= TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.AlphaTexture, oldTexturesData?.AlphaTexture, ref materialComponent.AlphaTexPromise, partitionComponent)
                       || !materialComponent.Data.Textures.AlphaTexture.HasValue;

            if (materialComponent.Data.IsPbrMaterial)
            {
                success &= TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.EmissiveTexture, oldTexturesData?.EmissiveTexture, ref materialComponent.EmissiveTexPromise, partitionComponent)
                           || !materialComponent.Data.Textures.EmissiveTexture.HasValue;

                success &= TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.BumpTexture, oldTexturesData?.BumpTexture, ref materialComponent.BumpTexPromise, partitionComponent)
                           || !materialComponent.Data.Textures.BumpTexture.HasValue;
            }

            return success;
        }

        private static MaterialData CreateBasicMaterialData(in PBMaterial pbMaterial, in TextureComponent? albedoTexture, in TextureComponent? alphaTexture) =>
            MaterialData.CreateBasicMaterial(albedoTexture, alphaTexture, pbMaterial.GetAlphaTest(), pbMaterial.GetDiffuseColor(), pbMaterial.GetCastShadows());

        private bool TryCreateGetTexturePromise(
            Entity entity,
            in TextureComponent? textureComponent,
            in TextureComponent? oldTextureComponent,
            ref Promise? promise,
            PartitionComponent partitionComponent
        )
        {
            if (!textureComponent.HasValue)
            {
                // If component is being reused forget the previous promise
                ReleaseMaterial.ReleaseIntention(entity, World, ref promise, true);
                return false;
            }

            TextureComponent textureComponentValue = textureComponent.Value;

            // If data inside promise has not changed just reuse the same promise
            // as creating and waiting for a new one can be expensive
            if (TextureComponentsAreEqual(in oldTextureComponent, in textureComponentValue))
                return false;

            // If component is being reused forget the previous promise
            ReleaseMaterial.ReleaseIntention(entity, World, ref promise, true);

            // TODO this code must be unified to be able to load video textures in a common way
            if (textureComponentValue.IsVideoTexture)
            {
                var intention = new GetTextureIntention(textureComponentValue.VideoPlayerEntity);

                bool foundConsumeEntity = textureComponentValue.TryAddConsumer(entity, entitiesMap, videoTexturesPool, World, out var info);
                if (!foundConsumeEntity) return false;

                StreamableLoadingResult<Texture2DData> result = new StreamableLoadingResult<Texture2DData>(info.VideoTexture!);

                promise = Promise.CreateFinalized(intention, result);

                if (info.VideoRenderer)
                    World.Add(info.VideoPlayer, new InitializeVideoPlayerMaterialRequest { Renderer = info.VideoRenderer });
            }
            else
                promise = Promise.Create(
                    World!,
                    new GetTextureIntention(
                        textureComponentValue.Src,
                        textureComponentValue.FileHash,
                        textureComponentValue.WrapMode,
                        textureComponentValue.FilterMode,
                        textureComponentValue.TextureType,
                        attemptsCount: attemptsCount,
                        isAvatarTexture: textureComponentValue.IsAvatarTexture,
                        reportSource: nameof(StartMaterialsLoadingSystem)
                    ),
                    partitionComponent
                );

            return true;
        }

        private static bool TextureComponentsAreEqual(in TextureComponent? oldTextureComponent, in TextureComponent textureComponent)
        {
            if (oldTextureComponent == null) return false; // nothing to do

            TextureComponent oldTextureValue = oldTextureComponent.Value;
            return textureComponent.Equals(oldTextureValue);
        }
    }
}
