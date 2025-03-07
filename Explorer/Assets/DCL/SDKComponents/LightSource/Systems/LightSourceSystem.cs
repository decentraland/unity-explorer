using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.ColorComponent;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly IPartitionComponent partitionComponent;
        private readonly IComponentPool<Light> poolRegistry;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ISceneData sceneData;

        public LightSourceSystem(World world,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPartitionComponent partitionComponent,
            IComponentPool<Light> poolRegistry
        ) : base(world)
        {
            this.partitionComponent = partitionComponent;
            this.sceneData = sceneData;
            this.poolRegistry = poolRegistry;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            CreateLightSourceComponentQuery(World);
            UpdateLightSourceQuery(World);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        [All(typeof(PBLightSource))]
        [None(typeof(LightSourceComponent))]
        private void CreateLightSourceComponent(in Entity entity, ref PBLightSource pbLightSource, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None) return;

            Light lightSourceInstance = poolRegistry.Get();

            lightSourceInstance.transform.SetParent(transform.Transform, false);
            lightSourceInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            lightSourceInstance.transform.localScale = Vector3.one;

            var lightSourceComponent = new LightSourceComponent(lightSourceInstance);
            pbLightSource.IsDirty = true;
            World.Add(entity, lightSourceComponent);
        }

        [Query]
        private void UpdateLightSource(ref LightSourceComponent lightSourceComponent, in PBLightSource pbLightSource)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if (!pbLightSource.IsDirty) return;
            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None) return;

            Light lightSourceInstance = lightSourceComponent.lightSourceInstance;

            bool isActive = !pbLightSource.HasActive || pbLightSource.Active;

            // No need to set anything if the component is not active
            if (!isActive)
            {
                lightSourceInstance.enabled = false;
                return;
            }

            bool isSpot = pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot;

            lightSourceInstance.type = isSpot ? LightType.Spot : LightType.Point;

            lightSourceInstance.color = pbLightSource.Color.ToUnityColor();

            if (pbLightSource.HasBrightness)
                lightSourceInstance.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);

            if (pbLightSource.HasRange)
                lightSourceInstance.range = pbLightSource.Range;

            if (isSpot)
            {
                if (pbLightSource.Spot.HasShadow)
                    lightSourceInstance.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Spot.Shadow);

                if (pbLightSource.Spot.HasInnerAngle)
                    lightSourceInstance.innerSpotAngle = pbLightSource.Spot.InnerAngle;

                if (pbLightSource.Spot.HasOuterAngle)
                    lightSourceInstance.spotAngle = pbLightSource.Spot.OuterAngle;

                bool usesShadowMask = pbLightSource.Spot.ShadowMaskTexture is { Texture: { Src: var s } } && !string.IsNullOrWhiteSpace(s);

                if (usesShadowMask)
                {
                    TextureComponent? shadowTexture = pbLightSource.Spot.ShadowMaskTexture.CreateTextureComponent(sceneData);
                    TryCreateGetTexturePromise(ref lightSourceComponent, in shadowTexture);
                }
                else
                {
                    CleanupPromise(ref lightSourceComponent);
                    lightSourceInstance.cookie = null;
                }
            }
            else { lightSourceInstance.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Point.Shadow); }

            lightSourceInstance.enabled = true;
        }

        private bool TryCreateGetTexturePromise(ref LightSourceComponent lightSourceComponent, in TextureComponent? textureComponent)
        {
            if (textureComponent == null)
                return false;

            TextureComponent textureComponentValue = textureComponent.Value;
            var intention = new GetTextureIntention(
                textureComponentValue.Src,
                textureComponentValue.FileHash,
                textureComponentValue.WrapMode,
                textureComponentValue.FilterMode,
                textureComponentValue.TextureType,
                attemptsCount: ATTEMPTS_COUNT
            );

            if (TextureComponentUtils.Equals(textureComponentValue, intention))
                return false;

            CleanupPromise(ref lightSourceComponent);

            lightSourceComponent.TextureMaskPromise = Promise.Create(
                World,
                intention,
                partitionComponent
            );

            return true;
        }

        [Query]
        private void ResolveTexturePromise(ref LightSourceComponent lightSourceComponent)
        {
            if (lightSourceComponent.TextureMaskPromise is null || lightSourceComponent.TextureMaskPromise.Value.IsConsumed) return;

            if (lightSourceComponent.TextureMaskPromise.Value.TryGetResult(World, out StreamableLoadingResult<Texture2DData> texture))
                lightSourceComponent.lightSourceInstance.cookie = texture.Asset;
        }

        private void CleanupPromise(ref LightSourceComponent lightSourceComponent)
        {
            if (lightSourceComponent.TextureMaskPromise == null)
                return;

            Promise promiseValue = lightSourceComponent.TextureMaskPromise.Value;
            promiseValue.ForgetLoading(World);
            promiseValue.TryDereference(World);
            promiseValue.Consume(World);
            lightSourceComponent.TextureMaskPromise = null;
        }

        [Query]
        private void FinalizeLightSourceComponents(in LightSourceComponent lightSourceComponent)
        {

            poolRegistry.Release(lightSourceComponent.lightSourceInstance);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeLightSourceComponentsQuery(World);
        }
    }
}
