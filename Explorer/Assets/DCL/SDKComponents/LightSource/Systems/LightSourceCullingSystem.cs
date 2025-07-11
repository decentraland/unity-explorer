using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Pool;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Handles culling light sources based on their distance to the player.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourcePreCullingUpdateSystem))]
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
            ClearLightSourceCullingQuery(World);
            ComputeDistanceToPlayerQuery(World);
            SortAndCullLightSources();
        }

        [Query]
        private void ClearLightSourceCulling(ref LightSourceComponent lightSourceComponent)
        {
            lightSourceComponent.Index = -1;
            lightSourceComponent.Rank = -1;
            lightSourceComponent.Culling = LightSourceComponent.CullingFlags.None;
        }

        [Query]
        private void ComputeDistanceToPlayer(in TransformComponent transform, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource)) return;

            lightSourceComponent.DistanceToPlayer = math.distance(transform.Transform.position, characterObject.Position);
        }

        private void SortAndCullLightSources()
        {
            _ = ListPool<LightSourceComponent>.Get(out var activeLights);
            CollectActiveLightSourcesQuery(World, activeLights);

            int maxLightCount = math.min((int)math.floor(sceneData.Parcels.Count * LIGHTS_PER_PARCEL), SCENE_MAX_LIGHT_COUNT);

            if (activeLights.Count <= maxLightCount) return;

            var distances = new NativeArray<float>(activeLights.Count, Allocator.Temp);
            for (var i = 0; i < distances.Length; i++) distances[i] = activeLights[i].DistanceToPlayer;

            SortByDistanceToPlayer(distances, out var ranks);

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
        private static void SortByDistanceToPlayer(in NativeArray<float> lightDistances, out NativeArray<int> ranks)
        {
            int lightCount = lightDistances.Length;

            var sortedIndices = new NativeArray<int>(lightCount, Allocator.Temp);
            for (var i = 0; i < lightCount; i++) sortedIndices[i] = i;

            sortedIndices.Sort(new DistanceToPlayerComparer(lightDistances));

            ranks = new NativeArray<int>(lightCount, Allocator.Temp);
            for (var i = 0; i < lightCount; i++) ranks[sortedIndices[i]] = i;
        }

        [Query]
        private void CullLightSources([Data] NativeArray<int> ranks, [Data] int maxLightCount, in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            // Lights without an index are either inactive or already culled by a previous system
            if (lightSourceComponent.Index < 0) return;

            lightSourceComponent.Rank = ranks[lightSourceComponent.Index];

            if (lightSourceComponent.Rank >= maxLightCount)
                lightSourceComponent.Culling |= LightSourceComponent.CullingFlags.TooManyLightSources;
        }

        #region DistanceToPlayerComparer

        /// <summary>
        /// Sorts lights from closest to more distant to the player position.
        /// It actually sorts an array of indices. Each index IDs a light in the distances array.
        /// </summary>
        private struct DistanceToPlayerComparer : IComparer<int>
        {
            public NativeArray<float> LightDistances;

            public DistanceToPlayerComparer(NativeArray<float> lightDistances)
            {
                LightDistances = lightDistances;
            }

            public int Compare(int lhs, int rhs)
            {
                return LightDistances[lhs].CompareTo(LightDistances[rhs]);
            }
        }

        #endregion
    }
}
