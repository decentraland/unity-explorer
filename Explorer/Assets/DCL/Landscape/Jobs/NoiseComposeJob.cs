using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct NoiseComposeJob : IJobParallelFor
    {
        private NativeArray<float> targetArray;
        [ReadOnly] private readonly NativeArray<float> noiseToCompose;
        [ReadOnly] private readonly NoiseJobOperation operation;

        public NoiseComposeJob(ref NativeArray<float> targetArray, NativeArray<float> noiseToCompose, NoiseJobOperation operation)
        {
            this.targetArray = targetArray;
            this.noiseToCompose = noiseToCompose;
            this.operation = operation;
        }

        public void Execute(int index)
        {
            float originalValue = targetArray[index];
            float composeValue = noiseToCompose[index];

            switch (operation)
            {
                case NoiseJobOperation.Set:
                    originalValue = composeValue; break;
                case NoiseJobOperation.Add:
                    originalValue += composeValue; break;
                case NoiseJobOperation.Multiply:
                    originalValue *= composeValue; break;
                case NoiseJobOperation.Subtract:
                    originalValue -= composeValue; break;
            }

            targetArray[index] = originalValue;
        }
    }
}
