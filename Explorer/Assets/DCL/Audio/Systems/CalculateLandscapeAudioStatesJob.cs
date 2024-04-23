﻿using DCL.Audio.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Audio.Jobs
{
    [BurstCompile]
    public struct CalculateLandscapeAudioStatesJob : IJobParallelFor
    {
        private NativeArray<LandscapeAudioState> terrainAudioStates;
        private readonly float2 cameraPosition;
        private readonly float audioListeningDistanceThreshold;
        private readonly float audioMutingDistanceThreshold;

        public CalculateLandscapeAudioStatesJob(
            NativeArray<LandscapeAudioState> terrainAudioStates,
            float2 cameraPosition,
            float audioListeningDistanceThreshold,
            float audioMutingDistanceThreshold)
        {
            this.terrainAudioStates = terrainAudioStates;
            this.cameraPosition = cameraPosition;
            this.audioMutingDistanceThreshold = audioMutingDistanceThreshold;
            this.audioListeningDistanceThreshold = audioListeningDistanceThreshold;
        }

        public void Execute(int i)
        {
            LandscapeAudioState landscapeAudioState = terrainAudioStates[i];
            TerrainAudioState terrainAudioState = landscapeAudioState.AudioState;

            float sqrDistance = math.distancesq(landscapeAudioState.CenterOfTerrain, cameraPosition);

            if (sqrDistance < audioListeningDistanceThreshold)
            {
                if (!terrainAudioState.IsHeard)
                {
                    terrainAudioState.ShouldBeHeard = true;
                    terrainAudioState.IsSilent = false;
                    terrainAudioState.ShouldBeSilent = false;
                }
            }

            //We do this so we are not removing AudioSources immediately after a player is out of range,
            //otherwise it might sound weird if player returns to a zone they just left
            else if (sqrDistance > audioMutingDistanceThreshold)
            {
                if (!terrainAudioState.IsSilent)
                {
                    terrainAudioState.IsHeard = false;
                    terrainAudioState.ShouldBeHeard = false;
                    terrainAudioState.ShouldBeSilent = true;
                }
            }

            landscapeAudioState.AudioState = terrainAudioState;

            terrainAudioStates[i] = landscapeAudioState;
        }
    }
}
