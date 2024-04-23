using DCL.Audio.Systems;
using System.Numerics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Audio.Jobs
{
    [BurstCompile]
    public struct CalculateOceanAudioStatesJob : IJobParallelFor
    {
        private NativeArray<OceanAudioState> oceanAudioStates;
        private readonly float3 cameraPosition;
        private readonly float oceanListeningDistanceThreshold;

        public CalculateOceanAudioStatesJob(
            NativeArray<OceanAudioState> oceanAudioStates,
            float3 cameraPosition,
            float oceanListeningDistanceThreshold
        )
        {
            this.oceanAudioStates = oceanAudioStates;
            this.cameraPosition = cameraPosition;
            this.oceanListeningDistanceThreshold = oceanListeningDistanceThreshold;
        }

        public void Execute(int i)
        {
            OceanAudioState oceanAudioState = oceanAudioStates[i];
            TerrainAudioState terrainAudioState = oceanAudioState.AudioState;

            float sqrDistance = oceanAudioState.Bounds.SqrDistance(cameraPosition);

            if (sqrDistance < oceanListeningDistanceThreshold)
            {
                Vector3 closestPoint = oceanAudioState.Bounds.ClosestPoint(cameraPosition);
                oceanAudioState.ClosestPoint = new int2((int)closestPoint.x, (int)closestPoint.z);

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
