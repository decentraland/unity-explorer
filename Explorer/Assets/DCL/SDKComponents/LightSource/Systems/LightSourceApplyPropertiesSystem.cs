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
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.ColorComponent;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using System;
using UnityEngine;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Updates the properties of existing light sources.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourceLifecycleSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceApplyPropertiesSystem : BaseUnityLoopSystem
    {
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

        private readonly ISceneData sceneData;
        private readonly IPartitionComponent partitionComponent;
        private readonly LightSourceSettings settings;

        public LightSourceApplyPropertiesSystem(World world, ISceneData sceneData, IPartitionComponent partitionComponent, LightSourceSettings settings) : base(world)
        {
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            UpdateLightSourceQuery(World);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        private void UpdateLightSource(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            bool isActive = LightSourceHelper.IsPBLightSourceActive(pbLightSource, settings.DefaultValues.Active);
            lightSourceInstance.enabled = isActive;

            // NOTE we reset the shadow quality every frame because culling and LOD systems can change it
            if (isActive)
            {
                bool shadows = pbLightSource.HasShadow ? pbLightSource.Shadow : settings.DefaultValues.Shadows;
                lightSourceInstance.shadows = shadows ? LightShadows.Soft :  LightShadows.None;
            }

            if (pbLightSource.IsDirty) ApplyPBLightSource(pbLightSource, ref lightSourceComponent);
        }

        private void ApplyPBLightSource(PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            var lightSourceInstance = lightSourceComponent.LightSourceInstance;

            if (pbLightSource.Color != null)
                lightSourceInstance.color = pbLightSource.Color.ToUnityColor();

            float intensity = pbLightSource.HasIntensity ? pbLightSource.Intensity : settings.DefaultValues.Intensity;
            float intensityScale = pbLightSource.TypeCase switch
                                   {
                                       PBLightSource.TypeOneofCase.Spot => settings.SpotLightIntensityScale,
                                       PBLightSource.TypeOneofCase.Point => settings.PointLightIntensityScale,
                                       _ => 1
                                   };
            intensity *= intensityScale;
            lightSourceComponent.MaxIntensity = PrimitivesConversionExtensions.PBIntensityInLumensToUnityCandels(intensity);

            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    ApplySpotLight(pbLightSource, lightSourceInstance);
                    ApplyCookie(ref lightSourceComponent, pbLightSource.ShadowMaskTexture);
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    ApplyPointLight(pbLightSource, lightSourceInstance);
                    break;
            }
        }

        private void ApplySpotLight(PBLightSource pbLightSource, Light light)
        {
            light.type = LightType.Spot;

            if (pbLightSource.Spot.HasInnerAngle)
                light.innerSpotAngle = pbLightSource.Spot.InnerAngle;

            if (pbLightSource.Spot.HasOuterAngle)
                light.spotAngle = pbLightSource.Spot.OuterAngle;
        }

        private static void ApplyPointLight(PBLightSource pbLightSource, Light light)
        {
            light.type = LightType.Point;
        }

        private void ApplyCookie(ref LightSourceComponent component, TextureUnion cookieTexture)
        {
            bool usesShadowMask = cookieTexture is { Texture: { Src: var s } } && !string.IsNullOrWhiteSpace(s);

            if (!usesShadowMask)
            {
                // Maybe be we were using a cookie, we need to dispose of it
                component.Cookie.CleanUp(World);
                component.LightSourceInstance.cookie = null;
                return;
            }

            TextureComponent? shadowTexture = cookieTexture.CreateTextureComponent(sceneData);
            if (shadowTexture != null) PrepareCookie(in shadowTexture, ref component.Cookie);
        }

        private void PrepareCookie(in TextureComponent? textureComponent, ref LightSourceComponent.CookieInfo cookie)
        {
            TextureComponent textureComponentValue = textureComponent!.Value;

            // Still loading the same texture OR the same cookie is already applied
            if (TextureComponentUtils.Equals(textureComponentValue, cookie.LoadingIntention)) return;

            // Dispose of the existing cookie we might have, since it has changed
            cookie.CleanUp(World);

            var intention = new GetTextureIntention(
                textureComponentValue.Src,
                textureComponentValue.FileHash,
                textureComponentValue.WrapMode,
                textureComponentValue.FilterMode,
                textureComponentValue.TextureType,
                nameof(LightSourceApplyPropertiesSystem),
                attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT);

            cookie.LoadingIntention = intention;
            cookie.LoadingPromise = TexturePromise.Create(World, intention, partitionComponent);
        }

        [Query]
        private void ResolveTexturePromise(ref LightSourceComponent lightSourceComponent)
        {
            var promise = lightSourceComponent.Cookie.LoadingPromise;

            if (promise is null || promise.Value.IsConsumed || !promise.Value.TryConsume(World, out StreamableLoadingResult<Texture2DData> texture)) return;

            // Clear the promise but keep the intention so we can compare it later on when updating the light source properties
            // Especially important when no-cache is used for textures (scene dev mode)
            lightSourceComponent.Cookie.LoadingPromise = null;
            lightSourceComponent.Cookie.SourceTextureData = texture.Asset;

            switch (lightSourceComponent.LightSourceInstance.type)
            {
                case LightType.Spot:
                    lightSourceComponent.LightSourceInstance.cookie = texture.Asset;
                    break;

                case LightType.Point:
                    Cubemap cubemap = MakeCookieCubemap(texture.Asset);
                    lightSourceComponent.LightSourceInstance.cookie = cubemap;
                    lightSourceComponent.Cookie.PointLightCubemap = cubemap;
                    break;

                default:
                    lightSourceComponent.LightSourceInstance.cookie = null;
                    break;
            }
        }

        private Cubemap MakeCookieCubemap(Texture2DData source)
        {
            // TODO

            return null;
        }
    }
}
