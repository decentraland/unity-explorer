using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using JetBrains.Annotations;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Handles culling light sources based on their distance to the player.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourceApplyPropertiesSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    [BurstCompile]
    public partial class LightSourceCullingSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly ICharacterObject characterObject;
        private readonly LightSourceSettings settings;

        public LightSourceCullingSystem(World world, ISceneData sceneData, ICharacterObject characterObject, LightSourceSettings settings) : base(world)
        {
            this.sceneData = sceneData;
            this.characterObject = characterObject;
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            ClearLightSourceCullingQuery(World);
            ComputeDistanceToPlayerQuery(World);
            SortAndCullLightSources();
        }

        [Query]
        private void ClearLightSourceCulling(ref LightSourceComponent lightSourceComponent)
        {
            lightSourceComponent.Index = -1;
            lightSourceComponent.Rank = -1;
            lightSourceComponent.TypeRank = -1;
            lightSourceComponent.Culling = LightSourceComponent.CullingFlags.None;
        }

        [Query]
        private void ComputeDistanceToPlayer(in TransformComponent transform, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource, settings.DefaultValues.Active)) return;

            lightSourceComponent.DistanceToPlayer = math.distance(transform.Transform.position, characterObject.Position);
        }

        private void SortAndCullLightSources()
        {
            const int CAPACITY = 1000;
            var lightData = new NativeList<LightData>(CAPACITY, Allocator.Temp);
            CollectActiveLightSourcesQuery(World, ref lightData);

            SortByDistanceToPlayer(lightData, out var ranks);

            int maxLightCount = math.min((int)math.floor(sceneData.Parcels.Count * settings.LightsPerParcel), settings.HardMaxLightCount);
            CullLightSourcesQuery(World, ranks, maxLightCount);
        }

        [Query]
        private void CollectActiveLightSources([Data] ref NativeList<LightData> lightData, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource, settings.DefaultValues.Active)) return;

            lightSourceComponent.Index = lightData.Length;
            lightData.AddNoResize(new LightData(pbLightSource.TypeCase, lightSourceComponent.DistanceToPlayer));
        }

        [BurstCompile]
        private static void SortByDistanceToPlayer(in NativeList<LightData> lightData, out NativeArray<(int, int)> ranks)
        {
            int lightCount = lightData.Length;

            var sortedIndices = new NativeArray<int>(lightCount, Allocator.Temp);
            for (var i = 0; i < lightCount; i++) sortedIndices[i] = i;

            sortedIndices.Sort(new DistanceToPlayerComparer(lightData));

            ranks = new NativeArray<(int, int)>(lightCount, Allocator.Temp);

            var pointLightRank = 0;
            var spotLightRank = 0;

            for (var i = 0; i < lightCount; i++)
            {
                int typeRank = lightData[i].Type switch
                               {
                                   PBLightSource.TypeOneofCase.Point => pointLightRank++,
                                   PBLightSource.TypeOneofCase.Spot => spotLightRank++,
                                   _ => -1
                               };

                ranks[sortedIndices[i]] = (i, typeRank);
            }
        }

        [Query]
        private void CullLightSources([Data] NativeArray<(int, int)> ranks, [Data] int maxLightCount, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            // Lights without an index are inactive
            if (lightSourceComponent.Index < 0) return;

            (int rank, int typeRank) = ranks[lightSourceComponent.Index];

            lightSourceComponent.Rank = rank;
            lightSourceComponent.TypeRank = typeRank;

            if (lightSourceComponent.Rank >= maxLightCount)
                lightSourceComponent.Culling |= LightSourceComponent.CullingFlags.TooManyLightSources;

            bool shouldDisableShadows = (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Point && lightSourceComponent.TypeRank >= settings.MaxPointLightShadows) ||
                                        (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot && lightSourceComponent.TypeRank >= settings.MaxSpotLightShadows);
            if (shouldDisableShadows)
                lightSourceComponent.LightSourceInstance.shadows = LightShadows.None;
        }

        public struct LightData
        {
            public PBLightSource.TypeOneofCase Type;

            public float Distance;

            public LightData(PBLightSource.TypeOneofCase type, float distance)
            {
                Type = type;
                Distance = distance;
            }
        }

        #region DistanceToPlayerComparer

        /// <summary>
        /// Sorts lights from closest to more distant to the player position.
        /// It actually sorts an array of indices. Each index IDs a light in the light data collection.
        /// </summary>
        private struct DistanceToPlayerComparer : IComparer<int>
        {
            public NativeList<LightData> LightData;

            public DistanceToPlayerComparer(NativeList<LightData> lightData)
            {
                LightData = lightData;
            }

            public int Compare(int lhs, int rhs)
            {
                return LightData[lhs].Distance.CompareTo(LightData[rhs].Distance);
            }
        }

        #endregion
    }
}
