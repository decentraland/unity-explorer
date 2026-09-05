using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct NoiseCutOffJob : IJobParallelFor
    {
        private NativeArray<float> targetArray;
        [ReadOnly] private readonly float finalCutOff;

        public NoiseCutOffJob(NativeArray<float> targetArray, float finalCutOff)
        {
            this.targetArray = targetArray;
            this.finalCutOff = finalCutOff;
        }

        public void Execute(int index)
        {
            float value = targetArray[index];

            if (value < finalCutOff)
                value = 0;

            targetArray[index] = value;
        }
    }
}
