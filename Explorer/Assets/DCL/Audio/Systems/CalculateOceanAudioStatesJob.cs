using DCL.Audio.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Audio.Jobs
{
    [BurstCompile]
    public struct CalculateOceanAudioStatesJob : IJobParallelFor
    {
        private NativeArray<LandscapeAudioState> oceanAudioStates;
        private readonly float2 cameraPosition;
        private readonly float oceanListeningDistanceThreshold;

        public CalculateOceanAudioStatesJob(
            NativeArray<LandscapeAudioState> oceanAudioStates,
            float2 cameraPosition,
            float oceanListeningDistanceThreshold
        )
        {
            this.oceanAudioStates = oceanAudioStates;
            this.cameraPosition = cameraPosition;
            this.oceanListeningDistanceThreshold = oceanListeningDistanceThreshold;
        }

        public void Execute(int i)
        {
            LandscapeAudioState oceanAudioState = oceanAudioStates[i];
            TerrainAudioState terrainAudioState = oceanAudioState.AudioState;

            float sqrDistance = math.distancesq(oceanAudioState.CenterOfTerrain, cameraPosition);

            if (sqrDistance < oceanListeningDistanceThreshold)
            {
                if (!terrainAudioState.IsHeard)
                {
                    terrainAudioState.ShouldBeHeard = true;
                    terrainAudioState.IsSilent = false;
                    terrainAudioState.ShouldBeSilent = false;
                }
            }
            else if (!terrainAudioState.IsSilent)
            {
                terrainAudioState.IsHeard = false;
                terrainAudioState.ShouldBeHeard = false;
                terrainAudioState.ShouldBeSilent = true;
            }

            oceanAudioState.AudioState = terrainAudioState;

            oceanAudioStates[i] = oceanAudioState;
        }
    }
}
