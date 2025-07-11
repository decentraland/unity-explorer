using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Pool;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Handles culling light sources (enabling / disabling them).
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourceLifecycleSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    [BurstCompile]
    public partial class LightSourceCullingSystem : BaseUnityLoopSystem
    {
        private const float LIGHTS_PER_PARCEL = 1;
        private const int SCENE_MAX_LIGHT_COUNT = 10;

        private readonly ISceneData sceneData;
        private readonly ICharacterObject characterObject;

        public LightSourceCullingSystem(World world, ISceneData sceneData, ICharacterObject characterObject) : base(world)
        {
            this.sceneData = sceneData;
            this.characterObject = characterObject;
        }

        protected override void Update(float t)
        {
            SortAndCullLightSources();
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
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource)) return;

            lightSourceComponent.Index = lights.Count;
            lights.Add(lightSourceComponent);
        }

        [BurstCompile]
        private static void SortByDistanceToPlayer(in float3 playerPosition, in NativeArray<float3> lightPositions, out NativeArray<int> ranks)
        {
            int lightCount = lightPositions.Length;

            var sortedIndices = new NativeArray<int>(lightCount, Allocator.Temp);
            for (var i = 0; i < lightCount; i++) sortedIndices[i] = i;

            sortedIndices.Sort(new DistanceToPlayerComparer(playerPosition, lightPositions));

            ranks = new NativeArray<int>(lightCount, Allocator.Temp);

            for (var i = 0; i < lightCount; i++) { ranks[sortedIndices[i]] = i; }
        }

        [Query]
        private void CullLightSources([Data] NativeArray<int> ranks, [Data] int maxLightCount, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource)) return;

            lightSourceComponent.Rank = ranks[lightSourceComponent.Index];
            lightSourceComponent.IsCulled = lightSourceComponent.Rank >= maxLightCount;
        }

        [Query]
        private void ClearLightSourceCulling(ref LightSourceComponent lightSourceComponent)
        {
            lightSourceComponent.Rank = -1;
            lightSourceComponent.IsCulled = false;
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
