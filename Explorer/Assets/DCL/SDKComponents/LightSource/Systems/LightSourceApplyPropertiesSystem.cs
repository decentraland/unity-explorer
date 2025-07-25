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
using JetBrains.Annotations;
using SceneRunner.Scene;
using System;
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
                nameof(LightSourceApplyPropertiesSystem),
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
