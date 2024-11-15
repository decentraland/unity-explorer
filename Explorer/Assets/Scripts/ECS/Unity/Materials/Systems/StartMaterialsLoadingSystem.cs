using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
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

            InvalidatePrbInequality(ref materialComponent, ref materialData);

            MaterialData.TexturesData prevTextureData = materialComponent.Data.Textures;
            materialComponent.Data = materialData;
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            World.Set(entity, StartNewMaterialLoad(entity, materialComponent, in prevTextureData, partitionComponent));
            World.AddOrGet(entity, new ShouldInstanceMaterialComponent());
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

        private MaterialComponent StartNewMaterialLoad(Entity entity, MaterialComponent materialComponent, in MaterialData.TexturesData prevTexturesData, PartitionComponent partitionComponent)
        {
            CreateGetTexturePromises(entity, ref materialComponent, prevTexturesData, partitionComponent);
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
            CreateGetTexturePromises(entity, ref materialComponent, null, partitionComponent);
            materialComponent.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            World.Add(entity, materialComponent);
            World.Add(entity, new ShouldInstanceMaterialComponent());
        }

        private MaterialData CreateMaterialData(in PBMaterial material)
        {
            if (material.Unlit != null)
                return CreateBasicMaterialData(material, albedoTexture: material.Unlit.Texture.CreateTextureComponent(sceneData), material.Unlit.AlphaTexture.CreateTextureComponent(sceneData));

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

        private void CreateGetTexturePromises(Entity entity, ref MaterialComponent materialComponent,
            in MaterialData.TexturesData? oldTexturesData,
            PartitionComponent partitionComponent)
        {
            TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.AlbedoTexture, oldTexturesData?.AlbedoTexture, ref materialComponent.AlbedoTexPromise, partitionComponent);

            if (materialComponent.Data.IsPbrMaterial)
            {
                TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.AlphaTexture, oldTexturesData?.AlphaTexture, ref materialComponent.AlphaTexPromise, partitionComponent);
                TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.EmissiveTexture, oldTexturesData?.EmissiveTexture, ref materialComponent.EmissiveTexPromise, partitionComponent);
                TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.BumpTexture, oldTexturesData?.BumpTexture, ref materialComponent.BumpTexPromise, partitionComponent);
            }
            else
            {
                TryCreateGetTexturePromise(entity, in materialComponent.Data.Textures.AlphaTexture, oldTexturesData?.AlphaTexture, ref materialComponent.AlphaTexPromise, partitionComponent);
            }
        }

        private static MaterialData CreateBasicMaterialData(in PBMaterial pbMaterial, in TextureComponent? albedoTexture, in TextureComponent? alphaTexture) =>
            MaterialData.CreateBasicMaterial(albedoTexture, alphaTexture, pbMaterial.GetAlphaTest(), pbMaterial.GetDiffuseColor(), pbMaterial.GetCastShadows());

        private bool TryCreateGetTexturePromise(Entity entity, in TextureComponent? textureComponent,
            in TextureComponent? oldTextureComponent,
            ref Promise? promise, PartitionComponent partitionComponent)
        {
            if (textureComponent == null)
            {
                // If component is being reused forget the previous promise
                ReleaseMaterial.ReleaseIntention(World, ref promise, true);
                return false;
            }

            TextureComponent textureComponentValue = textureComponent.Value;

            // If data inside promise has not changed just reuse the same promise
            // as creating and waiting for a new one can be expensive
            if (TextureComponentsAreEqual(in oldTextureComponent, in textureComponentValue))
                return false;

            // If component is being reused forget the previous promise
            ReleaseMaterial.ReleaseIntention(World, ref promise, true);

            // TODO this code must be unified to be able to load video textures in a common way
            if (textureComponentValue.IsVideoTexture)
            {
                var intention = new GetTextureIntention(textureComponentValue.VideoPlayerEntity);

                promise = Promise.CreateFinalized(intention,
                    textureComponentValue.TryAddConsumer(entity, entitiesMap, videoTexturesPool, World, out Texture2DData? tex)
                        ? new StreamableLoadingResult<Texture2DData>(tex!)
                        : new StreamableLoadingResult<Texture2DData>(GetReportCategory(), CreateException(new EcsEntityNotFoundException(textureComponentValue.VideoPlayerEntity, $"Entity {textureComponentValue.VideoPlayerEntity} not found!. VideoTexture will not be created."))));
            }
            else
                promise = Promise.Create(World!, new GetTextureIntention(textureComponentValue.Src,  textureComponentValue.FileHash, textureComponentValue.WrapMode, textureComponentValue.FilterMode, attemptsCount: attemptsCount), partitionComponent);

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
