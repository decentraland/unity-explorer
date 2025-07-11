using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Utils;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.ColorComponent;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using UnityEngine;
using Entity = Arch.Core.Entity;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Updates the properties of existing light sources.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourceLifecycleSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourcePreCullingUpdateSystem : BaseUnityLoopSystem
    {
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

        private readonly ISceneData sceneData;
        private readonly IPartitionComponent partitionComponent;

        public LightSourcePreCullingUpdateSystem(World world, ISceneData sceneData, IPartitionComponent partitionComponent) : base(world)
        {
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
        }

        protected override void Update(float t)
        {
            UpdateLightSourceQuery(World);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        private void UpdateLightSource(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!pbLightSource.IsDirty) return;

            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            lightSourceInstance.enabled = LightSourceHelper.IsPBLightSourceActive(pbLightSource);
            if (!lightSourceInstance.enabled) return;

            if (pbLightSource.IsDirty) ApplyPBLightSource(pbLightSource, ref lightSourceComponent);
        }

        private void ApplyPBLightSource(PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            var lightSourceInstance = lightSourceComponent.LightSourceInstance;

            lightSourceInstance.color = pbLightSource.Color.ToUnityColor();

            if (pbLightSource.HasBrightness)
                lightSourceInstance.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);

            if (pbLightSource.HasRange)
                lightSourceInstance.range = pbLightSource.Range;

            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    ApplySpotLight(pbLightSource, lightSourceInstance);
                    ApplyCookie(ref lightSourceComponent, pbLightSource.Spot.ShadowMaskTexture);
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    ApplyPointLight(pbLightSource, lightSourceInstance);
                    break;
            }
        }

        private void ApplySpotLight(PBLightSource pbLightSource, Light light)
        {
            light.type = LightType.Spot;

            if (pbLightSource.Spot.HasShadow)
                light.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Spot.Shadow);

            if (pbLightSource.Spot.HasInnerAngle)
                light.innerSpotAngle = pbLightSource.Spot.InnerAngle;

            if (pbLightSource.Spot.HasOuterAngle)
                light.spotAngle = pbLightSource.Spot.OuterAngle;
        }

        private static void ApplyPointLight(PBLightSource pbLightSource, Light light)
        {
            light.type = LightType.Point;

            if (pbLightSource.Point.HasShadow)
                light.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Point.Shadow);
        }

        private void ApplyCookie(ref LightSourceComponent component, TextureUnion cookie)
        {
            bool usesShadowMask = cookie is { Texture: { Src: var s } } && !string.IsNullOrWhiteSpace(s);

            if (!usesShadowMask)
            {
                component.LightSourceInstance.cookie = null;
                return;
            }

            TextureComponent? shadowTexture = cookie.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in shadowTexture, ref component.TextureMaskPromise);
        }

        private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref TexturePromise? promise)
        {
            if (textureComponent == null)
                return false;

            TextureComponent textureComponentValue = textureComponent.Value;

            if (TextureComponentUtils.Equals(ref textureComponentValue, ref promise)) return false;

            DereferenceTexture(ref promise);

            var intention = new GetTextureIntention(
                textureComponentValue.Src,
                textureComponentValue.FileHash,
                textureComponentValue.WrapMode,
                textureComponentValue.FilterMode,
                textureComponentValue.TextureType,
                attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT);

            promise = TexturePromise.Create(World, intention, partitionComponent);

            return true;
        }

        [Query]
        private void ResolveTexturePromise(in Entity entity, ref LightSourceComponent lightSourceComponent)
        {
            if (lightSourceComponent.TextureMaskPromise is null || lightSourceComponent.TextureMaskPromise.Value.IsConsumed) return;

            if (lightSourceComponent.TextureMaskPromise.Value.TryConsume(World, out StreamableLoadingResult<Texture2DData> texture))
            {
                lightSourceComponent.TextureMaskPromise = null;
                lightSourceComponent.LightSourceInstance.cookie = texture.Asset;
            }
        }

        private void DereferenceTexture(ref TexturePromise? promise)
        {
            if (promise == null) return;

            TexturePromise promiseValue = promise.Value;
            promiseValue.TryDereference(World);
        }
    }
}
