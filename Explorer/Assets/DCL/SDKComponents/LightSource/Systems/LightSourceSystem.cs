using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using DCL.Character;
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const float LIGHTS_PER_PARCEL = 1;
        private const int SCENE_MAX_LIGHT_COUNT = 10;
        private const float FADE_SPEED = 4;
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPartitionComponent partitionComponent;
        private readonly IComponentPool<Light> poolRegistry;
        private readonly ICharacterObject characterObject;

        public LightSourceSystem(World world,
            ISceneData sceneData,
            ISceneStateProvider sceneStateProvider,
            IPartitionComponent partitionComponent,
            IComponentPool<Light> poolRegistry,
            ICharacterObject characterObject
        ) : base(world)
        {
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.partitionComponent = partitionComponent;
            this.poolRegistry = poolRegistry;
            this.characterObject = characterObject;
        }

        protected override void Update(float t)
        {
            CreateLightSourceComponentQuery(World);
            UpdateLightSourceQuery(World);
            SortAndCullLightSources();
            AnimateLightSourceIntensityQuery(World, Time.unscaledDeltaTime);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        [None(typeof(LightSourceComponent))]
        private void CreateLightSourceComponent(in Entity entity, ref PBLightSource pbLightSource, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None)
            {
                ReportHub.LogWarning(GetReportCategory(), "Scene attempted to create a light source with type None");
                return;
            }

            Light lightSourceInstance = poolRegistry.Get();
            lightSourceInstance.intensity = 0;

            lightSourceInstance.transform.localScale = Vector3.one;
            lightSourceInstance.transform.SetParent(transform.Transform, false);

            float intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);
            var lightSourceComponent = new LightSourceComponent(lightSourceInstance, intensity);
            World.Add(entity, lightSourceComponent);

            pbLightSource.IsDirty = true;
        }

        [Query]
        private void UpdateLightSource(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!pbLightSource.IsDirty) return;

            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            lightSourceInstance.enabled = IsPBLightSourceActive(pbLightSource);
            if (!lightSourceInstance.enabled) return;

            if (pbLightSource.IsDirty) ApplyPBLightSource(pbLightSource, ref lightSourceComponent);
        }

        private void ApplyPBLightSource(PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            var lightSourceInstance = lightSourceComponent.LightSourceInstance;

            bool isSpotLight = pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot;
            lightSourceInstance.type = isSpotLight ? LightType.Spot : LightType.Point;

            lightSourceInstance.color = pbLightSource.Color.ToUnityColor();

            if (pbLightSource.HasBrightness)
                lightSourceInstance.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);

            if (pbLightSource.HasRange)
                lightSourceInstance.range = pbLightSource.Range;

            if (isSpotLight)
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
                    TryCreateGetTexturePromise(in shadowTexture, ref lightSourceComponent.TextureMaskPromise);
                }
                else
                {
                    lightSourceInstance.cookie = null;
                }
            }
            else
            {
                lightSourceInstance.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Point.Shadow);
            }
        }

        private void SortAndCullLightSources()
        {
            _ = ListPool<LightSourceComponent>.Get(out var activeLights);
            CollectActiveLightSourcesQuery(World, activeLights);

            int maxLightCount = math.min((int)math.floor(sceneData.Parcels.Count * LIGHTS_PER_PARCEL), SCENE_MAX_LIGHT_COUNT);

            if (activeLights.Count <= maxLightCount)
            {
                ClearLightSourceCullingQuery(World);
                return;
            }

            var positions = new NativeArray<float3>(activeLights.Count, Allocator.Temp);
            for (var i = 0; i < positions.Length; i++) positions[i] = activeLights[i].LightSourceInstance.transform.position;

            SortByDistanceToPlayer(characterObject.Position, positions, out var ranks);

            CullLightSourcesQuery(World, ranks, maxLightCount);
        }

        [Query]
        private void CollectActiveLightSources([Data] List<LightSourceComponent> lights, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!IsPBLightSourceActive(pbLightSource)) return;

            lightSourceComponent.Index = lights.Count;
            lights.Add(lightSourceComponent);
        }

        [BurstCompile]
        private static void SortByDistanceToPlayer(in float3 playerPosition, in NativeArray<float3> lightPositions, out NativeArray<int> ranks)
        {
            int lightCount = lightPositions.Length;

            var sortedIndices = new NativeArray<int>(lightCount,  Allocator.Temp);
            for (var i = 0; i < lightCount; i++) sortedIndices[i] = i;

            sortedIndices.Sort(new DistanceToPlayerComparer(playerPosition, lightPositions));

            ranks = new NativeArray<int>(lightCount, Allocator.Temp);
            for (var i = 0; i < lightCount; i++)
            {
                ranks[sortedIndices[i]] = i;
            }
        }

        [Query]
        private void CullLightSources([Data] NativeArray<int> ranks, [Data] int maxLightCount, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!IsPBLightSourceActive(pbLightSource)) return;

            lightSourceComponent.Rank = ranks[lightSourceComponent.Index];
            lightSourceComponent.IsCulled = lightSourceComponent.Rank >= maxLightCount;
        }

        [Query]
        private void ClearLightSourceCulling(ref LightSourceComponent lightSourceComponent)
        {
            lightSourceComponent.Rank = -1;
            lightSourceComponent.IsCulled = false;
        }

        [Query]
        private void AnimateLightSourceIntensity([Data] float dt, ref LightSourceComponent lightSourceComponent, in PBLightSource pbLightSource)
        {
            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            if (!IsPBLightSourceActive(pbLightSource))
            {
                lightSourceInstance.intensity = 0;
                return;
            }

            bool isLightOn = sceneStateProvider.IsCurrent && !lightSourceComponent.IsCulled;
            lightSourceComponent.TargetIntensity = isLightOn ? lightSourceComponent.MaxIntensity : 0;

            float delta = dt * lightSourceComponent.MaxIntensity * FADE_SPEED;
            lightSourceComponent.CurrentIntensity = Mathf.MoveTowards(lightSourceComponent.CurrentIntensity, lightSourceComponent.TargetIntensity, delta);

            lightSourceInstance.intensity = lightSourceComponent.CurrentIntensity;

            lightSourceInstance.enabled = lightSourceComponent.CurrentIntensity > 0;
        }

        private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise)
        {
            if (textureComponent == null)
                return false;

            TextureComponent textureComponentValue = textureComponent.Value;

            if (TextureComponentUtils.Equals(ref textureComponentValue, ref promise))
                return false;

            DereferenceTexture(ref promise);

            promise = Promise.Create(
                World,
                new GetTextureIntention(
                    textureComponentValue.Src,
                    textureComponentValue.FileHash,
                    textureComponentValue.WrapMode,
                    textureComponentValue.FilterMode,
                    textureComponentValue.TextureType,
                    attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT
                ),
                partitionComponent
            );

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

        private void DereferenceTexture(ref Promise? promise)
        {
            if (promise == null)
                return;

            Promise promiseValue = promise.Value;
            promiseValue.TryDereference(World);
        }

        [Query]
        private void ReleaseLightSources(in LightSourceComponent lightSourceComponent)
        {
            poolRegistry.Release(lightSourceComponent.LightSourceInstance);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseLightSourcesQuery(World);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPBLightSourceActive(in PBLightSource pbLightSource)
        {
            return !pbLightSource.HasActive || pbLightSource.Active;
        }

        #region DistanceToPlayerComparer

        /// <summary>
        /// Sorts lights from closest to more distant to the player position.
        /// It actually sorts an array of indices. Each index IDs a light in the positions array.
        /// </summary>
        private struct DistanceToPlayerComparer : IComparer<int>
        {
            public float3 PlayerPosition;

            public NativeArray<float3> LightPositions;

            public DistanceToPlayerComparer(float3 playerPosition, NativeArray<float3> lightPositions)
            {
                PlayerPosition = playerPosition;
                LightPositions = lightPositions;
            }

            public int Compare(int lhs, int rhs)
            {
                float lhsDistanceSq = math.distancesq(LightPositions[lhs], PlayerPosition);
                float rhsDistanceSq = math.distancesq(LightPositions[rhs], PlayerPosition);
                return lhsDistanceSq.CompareTo(rhsDistanceSq);
            }
        }

        #endregion
    }
}
