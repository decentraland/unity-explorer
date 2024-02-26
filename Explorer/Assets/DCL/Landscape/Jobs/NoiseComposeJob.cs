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
                case NoiseJobOperation.SET:
                    originalValue = composeValue; break;
                case NoiseJobOperation.ADD:
                    originalValue += composeValue; break;
                case NoiseJobOperation.MULTIPLY:
                    originalValue *= composeValue; break;
                case NoiseJobOperation.SUBTRACT:
                    originalValue -= composeValue; break;
            }

            targetArray[index] = originalValue;
        }
    }
}
