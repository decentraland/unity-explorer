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
using DCL.Landscape;
using DCL.Landscape.Systems;
using ECS;
using Random = UnityEngine.Random;

namespace DCL.Audio.Systems
{
    /// <summary>
    ///     This system updates the audio state for every terrain chunk calculating the distance to its center and determining if its in "hearing distance"
    ///     This is done through a parallel job. If it should be heard, we enable the audioSources on the pre-calculated positions setting them to play the corresponding audio for their zone.
    /// </summary>
    [LogCategory(ReportCategory.AUDIO)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeAudioCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly WorldTerrainGenerator worldTerrainGenerator;
        private readonly ILandscapeAudioSystemSettings landscapeAudioSystemSettings;
        private readonly WorldAudioPlaybackController worldAudioPlaybackController;
        private readonly IRealmData realmData;
        private bool isTerrainViewInitialized;

        private NativeArray<LandscapeAudioState> landscapeAudioStates;
        private NativeArray<NativeArray<int2>> landscapeAudioSourcesPositions;
        private NativeArray<LandscapeAudioState> oceanAudioStates;
        private JobHandle landscapeJobHandle;
        private JobHandle oceanJobHandle;
        private float audioListeningDistanceThreshold;
        private float audioMutingDistanceThreshold;
        private float oceanListeningDistanceThreshold;
        private ITerrain terrain;

        private int updateFramesCounter;

        private LandscapeAudioCullingSystem(World world,
            TerrainGenerator terrainGenerator,
            WorldTerrainGenerator worldTerrainGenerator,
            ILandscapeAudioSystemSettings landscapeAudioSystemSettings,
            WorldAudioPlaybackController worldAudioPlaybackController,
            IRealmData realmData) : base(world)
        {
            this.terrainGenerator = terrainGenerator;
            this.worldTerrainGenerator = worldTerrainGenerator;
            this.landscapeAudioSystemSettings = landscapeAudioSystemSettings;
            this.worldAudioPlaybackController = worldAudioPlaybackController;
            this.realmData = realmData;
        }

        public override void Initialize()
        {
            base.Initialize();

            oceanJobHandle = default(JobHandle);
            landscapeJobHandle = default(JobHandle);
        }

        protected override void OnDispose()
        {
            landscapeJobHandle.Complete();
            oceanJobHandle.Complete();
            landscapeAudioStates.Dispose();

            if (landscapeAudioSourcesPositions.IsCreated)
                for (var i = 0; i < landscapeAudioSourcesPositions.Length; i++)
                    landscapeAudioSourcesPositions[i].Dispose();

            landscapeAudioSourcesPositions.Dispose();

            oceanAudioStates.Dispose();
        }

        protected override void Update(float t)
        {
            ITerrain currentTerrain = realmData.IsGenesis() ? terrainGenerator : worldTerrainGenerator;
            if (currentTerrain != terrain)
            {
                if (isTerrainViewInitialized)
                    FlushPreviousAudioStates();

                terrain = currentTerrain;
                isTerrainViewInitialized = false;
                updateFramesCounter = 0;
            }

            if (!terrain.IsTerrainShown) return;

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

        private void FlushPreviousAudioStates()
        {
            for (int i = 0; i < landscapeAudioStates.Length; i++)
                worldAudioPlaybackController.ReleaseAudioSourcesFromTerrain(i, WorldAudioClipType.Landscape);
            for (int i = 0; i < oceanAudioStates.Length; i++)
                worldAudioPlaybackController.ReleaseAudioSourcesFromTerrain(i, WorldAudioClipType.Ocean);
        }

        private void InitializeTerrainAudioStates()
        {
            int parcelSize = terrain.ParcelSize;
            ChunkModel[] terrainChunks = terrain.TerrainModel.ChunkModels;
            landscapeAudioStates = new NativeArray<LandscapeAudioState>(terrainChunks.Length, Allocator.Persistent);
            landscapeAudioSourcesPositions = new NativeArray<NativeArray<int2>>(terrainChunks.Length, Allocator.Persistent);
            int chunkSize = terrain.GetChunkSize();
            int halfTerrainChunkSize = chunkSize / 2;
            audioListeningDistanceThreshold = math.pow(halfTerrainChunkSize + landscapeAudioSystemSettings.ListeningDistanceThreshold, 2);
            audioMutingDistanceThreshold = math.pow(halfTerrainChunkSize + landscapeAudioSystemSettings.MutingDistanceThreshold, 2);

            for (var i = 0; i < terrainChunks.Length; i++)
            {
                ChunkModel chunk = terrainChunks[i];
                int2 center = (chunk.MinParcel * parcelSize) + halfTerrainChunkSize;

                landscapeAudioStates[i] = new LandscapeAudioState
                {
                    CenterOfTerrain = new float2(center.x, center.y),
                };

                landscapeAudioSourcesPositions[i] = CalculateAudioSourcesPositions(chunk, chunkSize, parcelSize);
            }
        }

        private void InitializeOceanAudioStates()
        {
            //To calculate the ocean audio, we will use the cliffs positions as the ocean grid does not match correctly with the world grid.
            IReadOnlyList<Transform> cliffs = terrain.Cliffs;

            oceanAudioStates = new NativeArray<LandscapeAudioState>(cliffs.Count, Allocator.Persistent);

            for (var i = 0; i < cliffs.Count; i++)
            {
                Transform cliff = cliffs[i];

                Vector3 position = cliff.transform.position;

                oceanAudioStates[i] = new LandscapeAudioState
                {
                    CenterOfTerrain = new float2(position.x, position.z),
                };
            }

            oceanListeningDistanceThreshold = math.pow(landscapeAudioSystemSettings.OceanDistanceThreshold, 2);
        }

        private NativeArray<int2> CalculateAudioSourcesPositions(ChunkModel terrain, int2 chunkSize, int parcelSize)
        {
            int rowsPerChunk = landscapeAudioSystemSettings.RowsPerChunk;
            int retryAttempts = landscapeAudioSystemSettings.AudioSourcePositioningRetryAttempts;
            int cellWidth = chunkSize.x / rowsPerChunk;
            int cellLength = chunkSize.y / rowsPerChunk;

            var positions = new NativeList<int2>(rowsPerChunk * rowsPerChunk, Allocator.Persistent);

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

                        if (!terrain.IsOutsideOrOccupied(terrain.MinParcel + (randomPosition / parcelSize)))
                        {
                            positions.Add((terrain.MinParcel * parcelSize) + randomPosition);
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
            if (landscapeJobHandle.IsCompleted && !landscapeJobHandle.Equals(default(JobHandle)))
            {
                Profiler.BeginSample("UpdateTerrainAudioEvents.Update");
                landscapeJobHandle.Complete();

                for (var i = 0; i < landscapeAudioStates.Length; i++)
                {
                    LandscapeAudioState landscapeAudioState = landscapeAudioStates[i];
                    TerrainAudioState audioState = landscapeAudioState.AudioState;

                    if (audioState is { ShouldBeHeard: true, IsHeard: false })
                    {
                        audioState.IsHeard = true;
                        landscapeAudioState.AudioState = audioState;
                        landscapeAudioStates[i] = landscapeAudioState;
                        worldAudioPlaybackController.SetupAudioSourcesOnTerrain(i, landscapeAudioSourcesPositions[i], WorldAudioClipType.Landscape);
                    }
                    else if (audioState is { ShouldBeSilent: true, IsSilent: false })
                    {
                        audioState.IsSilent = true;
                        landscapeAudioState.AudioState = audioState;
                        landscapeAudioStates[i] = landscapeAudioState;
                        worldAudioPlaybackController.ReleaseAudioSourcesFromTerrain(i, WorldAudioClipType.Landscape);
                    }
                }

                Profiler.EndSample();
            }

            // Schedule
            if (landscapeJobHandle.IsCompleted)
            {
                Profiler.BeginSample("CalculateLandscapeAudioStatesJob.Schedule");
                landscapeJobHandle.Complete();

                Vector3 position = cameraComponent.Camera.transform.position;
                var cameraPosition = new float2(position.x, position.z);

                var job = new CalculateLandscapeAudioStatesJob(landscapeAudioStates, cameraPosition, audioListeningDistanceThreshold, audioMutingDistanceThreshold);
                landscapeJobHandle = job.Schedule(landscapeAudioStates.Length, 32, landscapeJobHandle);
                Profiler.EndSample();
            }
        }

        [Query]
        private void UpdateOceanAudioEvents(in Entity _, in CameraComponent cameraComponent)
        {
            if (oceanJobHandle.IsCompleted && !oceanJobHandle.Equals(default(JobHandle)))
            {
                Profiler.BeginSample("UpdateOceanAudioEvents.Update");
                oceanJobHandle.Complete();

                for (var i = 0; i < oceanAudioStates.Length; i++)
                {
                    LandscapeAudioState oceanAudioState = oceanAudioStates[i];
                    TerrainAudioState audioState = oceanAudioState.AudioState;

                    if (audioState is { ShouldBeHeard: true, IsHeard: false })
                    {
                        audioState.IsHeard = true;
                        oceanAudioState.AudioState = audioState;
                        oceanAudioStates[i] = oceanAudioState;
                        var closestPointArray = new NativeArray<int2>(1, Allocator.Temp);
                        closestPointArray[0] = (int2)oceanAudioState.CenterOfTerrain;
                        worldAudioPlaybackController.SetupAudioSourcesOnTerrain(i, closestPointArray, WorldAudioClipType.Ocean);
                    }
                    else if (audioState is { ShouldBeSilent: true, IsSilent: false })
                    {
                        audioState.IsSilent = true;
                        oceanAudioState.AudioState = audioState;
                        oceanAudioStates[i] = oceanAudioState;
                        worldAudioPlaybackController.ReleaseAudioSourcesFromTerrain(i, WorldAudioClipType.Ocean);
                    }
                }

                Profiler.EndSample();
            }

            // Schedule
            if (oceanJobHandle.IsCompleted)
            {
                Profiler.BeginSample("CalculateOceanAudioStatesJob.Schedule");
                oceanJobHandle.Complete();

                Vector3 position = cameraComponent.Camera.transform.position;
                var cameraPosition = new float2(position.x, position.z);

                var job = new CalculateOceanAudioStatesJob(oceanAudioStates, cameraPosition, oceanListeningDistanceThreshold);
                oceanJobHandle = job.Schedule(oceanAudioStates.Length, 32, oceanJobHandle);
                Profiler.EndSample();
            }
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
}
