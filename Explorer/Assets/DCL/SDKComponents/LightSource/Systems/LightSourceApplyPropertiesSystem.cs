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
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

            lightSourceComponent.IntensityScale = pbLightSource.TypeCase switch
                                                  {
                                                      PBLightSource.TypeOneofCase.Spot => settings.SpotLightIntensityScale,
                                                      PBLightSource.TypeOneofCase.Point => settings.PointLightIntensityScale,
                                                      _ => 1
                                                  };
            lightSourceComponent.MaxIntensity = intensity;

            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    ApplySpotLight(pbLightSource, lightSourceInstance);
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    ApplyPointLight(pbLightSource, lightSourceInstance);
                    break;
            }

            ApplyCookie(ref lightSourceComponent, pbLightSource.ShadowMaskTexture);
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
                // Maybe we were using a cookie, we need to dispose of it
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
            var texture2d = source.Asset;

            int faceSize = texture2d.width / 4;
            if (texture2d.height != faceSize * 3)
            {
                ReportHub.LogError(GetReportCategory(), "Point Light cookie texture must be laid out in a 4x3 grid");
                return null;
            }
            int facePixelCount = faceSize * faceSize;

            NativeArray<Color32> sourceColors = texture2d.GetPixelData<Color32>(0);
            var destinationColors = new NativeArray<Color32>(facePixelCount * 6, Allocator.TempJob);

            new CubemapGenerationJob
            {
                SourceColors = sourceColors,
                DestinationColors = destinationColors,
                FaceSize = faceSize,
                FacePixelCount = faceSize * faceSize,
                SourceTextureWidth = texture2d.width,
            }.Schedule(destinationColors.Length, 64).Complete();

            Cubemap cubemap = new Cubemap(faceSize, texture2d.format, false);
            for (var face = 0; face < 6; face++)
                cubemap.SetPixelData(destinationColors, 0, (CubemapFace)face, face * facePixelCount);
            cubemap.Apply();

            destinationColors.Dispose();

            return cubemap;
        }

        [BurstCompile]
        private struct CubemapGenerationJob : IJobParallelFor
        {
            private static readonly int2[] TILES =
            {
                new (2, 1),
                new (0, 1),
                new (1, 2),
                new (1, 0),
                new (1, 1),
                new (3, 1)
            };

            [ReadOnly] public NativeArray<Color32> SourceColors;

            [WriteOnly] public NativeArray<Color32> DestinationColors;

            public int FaceSize;

            public int FacePixelCount;

            public int SourceTextureWidth;

            [BurstCompile]
            public void Execute(int index)
            {
                int2 tile = TILES[index / FacePixelCount];
                int facePixel = index % FacePixelCount;

                int sourceX = (tile.x * FaceSize) + (facePixel % FaceSize);
                int sourceY = (tile.y * FaceSize) + (FaceSize - 1 - (facePixel / FaceSize));

                int sourceIndex = math.mad(sourceY, SourceTextureWidth, sourceX);

                DestinationColors[index] = SourceColors[sourceIndex];
            }
        }
    }
}
