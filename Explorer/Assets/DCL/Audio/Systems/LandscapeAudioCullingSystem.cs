using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Vector3 = UnityEngine.Vector3;
using DCL.Audio.Jobs;
using DCL.Audio.System;
using DCL.Landscape;
using DCL.Landscape.Systems;
using System.Linq;
using Random = UnityEngine.Random;

namespace DCL.Audio.Systems
{
    /// <summary>
    ///     This system updates the audio state for every terrain chunk calculating the distance to its center and determining if its in "hearing distance"
    ///     This is done through a parallel job. If it should be heard, we enable the audioSources on the pre-calculated positions setting them to play the corresponding audio for their zone.
    /// </summary>
    [LogCategory(ReportCategory.AUDIO)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LandscapeTerrainCullingSystem))]
    public partial class LandscapeAudioCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly ILandscapeAudioSystemSettings landscapeAudioSystemSettings;
        private bool isTerrainViewInitialized;

        private NativeArray<LandscapeAudioState> landscapeAudioStates;
        private NativeArray<NativeArray<int2>> landscapeAudioSourcesPositions;
        private NativeArray<OceanAudioState> oceanAudioStates;
        private JobHandle jobHandle;
        private float audioListeningDistanceThreshold;
        private float audioMutingDistanceThreshold;
        private float oceanListeningDistanceThreshold;

        private int updateFramesCounter;

        private LandscapeAudioCullingSystem(World world,
            TerrainGenerator terrainGenerator,
            ILandscapeAudioSystemSettings landscapeAudioSystemSettings) : base(world)
        {
            this.terrainGenerator = terrainGenerator;
            this.landscapeAudioSystemSettings = landscapeAudioSystemSettings;
        }

        public override void Initialize()
        {
            base.Initialize();

            jobHandle = default(JobHandle);
        }

        public override void Dispose()
        {
            base.Dispose();
            jobHandle.Complete();
            landscapeAudioStates.Dispose();
            landscapeAudioSourcesPositions.Dispose();
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainGenerated()) return;

            if (updateFramesCounter <= 0)
            {
                if (!isTerrainViewInitialized)
                {
                    InitializeTerrainAudioStates();
                    InitializeOceanAudioStates();
                    isTerrainViewInitialized = true;
                }

                if (isTerrainViewInitialized)
                {
                    UpdateTerrainAudioEventsQuery(World);
                    UpdateOceanAudioEventsQuery(World);
                }

                updateFramesCounter = landscapeAudioSystemSettings.SystemUpdateFrequency;
            }
            else { updateFramesCounter--; }
        }

        private void InitializeTerrainAudioStates()
        {
            IReadOnlyList<Terrain> terrains = terrainGenerator.GetTerrains();
            landscapeAudioStates = new NativeArray<LandscapeAudioState>(terrains.Count, Allocator.Persistent);
            landscapeAudioSourcesPositions = new NativeArray<NativeArray<int2>>(terrains.Count, Allocator.Persistent);
            int halfTerrainChunkSize = terrainGenerator.GetChunkSize() / 2;
            audioListeningDistanceThreshold = math.pow(halfTerrainChunkSize + landscapeAudioSystemSettings.ListeningDistanceThreshold, 2);
            audioMutingDistanceThreshold = math.pow(halfTerrainChunkSize + landscapeAudioSystemSettings.MutingDistanceThreshold, 2);

            for (var i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                Bounds bounds = GetTerrainBoundsInWorldSpace(terrain);
                landscapeAudioSourcesPositions[i] = CalculateAudioSourcesPositions(terrain.terrainData, bounds);

                landscapeAudioStates[i] = new LandscapeAudioState
                {
                    CenterOfTerrain = new float2(bounds.center.x, bounds.center.z),
                };
            }
        }

        private void InitializeOceanAudioStates()
        {
            Transform ocean = terrainGenerator.GetOcean();
            MeshRenderer[] renderers = ocean.GetComponentsInChildren<MeshRenderer>();

            // some water chunks are disabled on purpose, we dont want to re-enable them
            var waterRenderers = renderers.Where(meshRenderer => meshRenderer.enabled).ToList();

            oceanAudioStates = new NativeArray<OceanAudioState>(waterRenderers.Count, Allocator.Persistent);

            for (var i = 0; i < waterRenderers.Count; i++)
            {
                MeshRenderer waterChunk = waterRenderers[i];
                Bounds bounds = waterChunk.bounds;

                oceanAudioStates[i] = new OceanAudioState
                {
                    Bounds = new Bounds
                    {
                        center = bounds.center,
                        extents = bounds.extents + Vector3.one,
                    },
                };
            }

            oceanListeningDistanceThreshold = math.pow(landscapeAudioSystemSettings.OceanDistanceThreshold, 2);
        }

        private NativeArray<int2> CalculateAudioSourcesPositions(TerrainData terrainData, Bounds worldBounds)
        {
            int rowsPerChunk = landscapeAudioSystemSettings.RowsPerChunk;
            int retryAttempts = landscapeAudioSystemSettings.AudioSourcePositioningRetryAttempts;
            Vector3 terrainSize = terrainData.size;
            int cellWidth = (int)terrainSize.x / rowsPerChunk;
            int cellLength = (int)terrainSize.z / rowsPerChunk;

            var positions = new NativeList<int2>(Allocator.Persistent);
            var worldCellMin = new int2((int)worldBounds.min.x, (int)worldBounds.min.z);

            for (var row = 0; row < rowsPerChunk; row++)
            {
                for (var col = 0; col < rowsPerChunk; col++)
                {
                    var localCellCenter = new int2(
                        (col * cellWidth) + (cellWidth / 2),
                        (row * cellLength) + (cellLength / 2)
                    );

                    for (var retry = 0; retry < retryAttempts; retry++)
                    {
                        var randomOffset = new int2(Random.Range(-cellWidth / 2, cellWidth / 2), Random.Range(-cellLength / 2, cellLength / 2));
                        int2 randomPosition = localCellCenter + randomOffset;

                        if (!terrainData.IsHole(randomPosition.x, randomPosition.y))
                        {
                            positions.Add(worldCellMin + randomPosition);
                            break;
                        }
                    }
                }
            }

            return positions.AsArray();
        }

        [Query]
        private void UpdateTerrainAudioEvents(in Entity _, in CameraComponent cameraComponent)
        {
            if (jobHandle.IsCompleted && !jobHandle.Equals(default(JobHandle)))
            {
                Profiler.BeginSample("UpdateTerrainAudioEvents.Update");
                jobHandle.Complete();

                for (var i = 0; i < landscapeAudioStates.Length; i++)
                {
                    LandscapeAudioState landscapeAudioState = landscapeAudioStates[i];
                    TerrainAudioState audioState = landscapeAudioState.AudioState;

                    if (audioState is { ShouldBeHeard: true, IsHeard: false })
                    {
                        audioState.IsHeard = true;
                        landscapeAudioState.AudioState = audioState;
                        landscapeAudioStates[i] = landscapeAudioState;
                        WorldAudioEventsBus.Instance.SendPlayTerrainAudioEvent(i, landscapeAudioSourcesPositions[i], WorldAudioClipType.Landscape);
                    }
                    else if (audioState is { ShouldBeSilent: true, IsSilent: false })
                    {
                        audioState.IsSilent = true;
                        landscapeAudioState.AudioState = audioState;
                        landscapeAudioStates[i] = landscapeAudioState;
                        WorldAudioEventsBus.Instance.SendStopTerrainAudioEvent(i, WorldAudioClipType.Landscape);
                    }
                }

                Profiler.EndSample();
            }

            // Schedule
            if (jobHandle.IsCompleted)
            {
                Profiler.BeginSample("CalculateLandscapeAudioStatesJob.Schedule");
                jobHandle.Complete();

                Vector3 position = cameraComponent.Camera.transform.position;
                var cameraPosition = new float2(position.x, position.z);

                var job = new CalculateLandscapeAudioStatesJob(landscapeAudioStates, cameraPosition, audioListeningDistanceThreshold, audioMutingDistanceThreshold);
                jobHandle = job.Schedule(landscapeAudioStates.Length, 32, jobHandle);
                Profiler.EndSample();
            }
        }

        [Query]
        private void UpdateOceanAudioEvents(in Entity _, in CameraComponent cameraComponent)
        {
            if (jobHandle.IsCompleted && !jobHandle.Equals(default(JobHandle)))
            {
                Profiler.BeginSample("UpdateOceanAudioEvents.Update");
                jobHandle.Complete();

                for (var i = 0; i < oceanAudioStates.Length; i++)
                {
                    OceanAudioState oceanAudioState = oceanAudioStates[i];
                    TerrainAudioState audioState = oceanAudioState.AudioState;

                    if (audioState is { ShouldBeHeard: true, IsHeard: false })
                    {
                        audioState.IsHeard = true;
                        oceanAudioState.AudioState = audioState;
                        oceanAudioStates[i] = oceanAudioState;
                        var closestPointArray = new NativeArray<int2>(1, Allocator.Temp);
                        closestPointArray[0] = oceanAudioState.ClosestPoint;
                        WorldAudioEventsBus.Instance.SendPlayTerrainAudioEvent(i, closestPointArray, WorldAudioClipType.Ocean);
                    }
                    else if (audioState.IsHeard)
                    {
                        var closestPointArray = new NativeArray<int2>(1, Allocator.Temp);
                        closestPointArray[0] = oceanAudioState.ClosestPoint;
                        WorldAudioEventsBus.Instance.SendPlayTerrainAudioEvent(i, closestPointArray, WorldAudioClipType.Ocean);
                    }
                    else if (audioState is { ShouldBeSilent: true, IsSilent: false })
                    {
                        audioState.IsSilent = true;
                        oceanAudioState.AudioState = audioState;
                        oceanAudioStates[i] = oceanAudioState;
                        WorldAudioEventsBus.Instance.SendStopTerrainAudioEvent(i, WorldAudioClipType.Ocean);
                    }
                }

                Profiler.EndSample();
            }

            // Schedule
            if (jobHandle.IsCompleted)
            {
                Profiler.BeginSample("CalculateOceanAudioStatesJob.Schedule");
                jobHandle.Complete();

                float3 position = cameraComponent.Camera.transform.position;

                var job = new CalculateOceanAudioStatesJob(oceanAudioStates, position, oceanListeningDistanceThreshold);
                jobHandle = job.Schedule(landscapeAudioStates.Length, 32, jobHandle);
                Profiler.EndSample();
            }
        }

        private Bounds GetTerrainBoundsInWorldSpace(Terrain terrain)
        {
            Bounds localBounds = terrain.terrainData.bounds;
            Vector3 terrainPosition = terrain.transform.position;
            var worldBounds = new Bounds(localBounds.center + terrainPosition, localBounds.size);
            return worldBounds;
        }
    }

    public struct TerrainAudioState
    {
        public bool IsSilent;
        public bool IsHeard;
        public bool ShouldBeSilent;
        public bool ShouldBeHeard;
    }

    public struct LandscapeAudioState
    {
        public TerrainAudioState AudioState;
        public float2 CenterOfTerrain;
    }

    public struct OceanAudioState
    {
        public TerrainAudioState AudioState;
        public Bounds Bounds;
        public int2 ClosestPoint;
    }
}
