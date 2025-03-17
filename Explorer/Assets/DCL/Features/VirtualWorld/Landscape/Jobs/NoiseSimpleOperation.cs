using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct NoiseSimpleOperation : IJobParallelFor
    {
        private NativeArray<float> targetArray;
        [ReadOnly] private readonly float value;
        [ReadOnly] private readonly NoiseJobOperation operation;

        public NoiseSimpleOperation(NativeArray<float> targetArray, float value, NoiseJobOperation operation)
        {
            this.targetArray = targetArray;
            this.value = value;
            this.operation = operation;
        }

        public void Execute(int index)
        {
            float originalValue = targetArray[index];

            switch (operation)
            {
                case NoiseJobOperation.SET:
                    originalValue = value; break;
                case NoiseJobOperation.ADD:
                    originalValue += value; break;
                case NoiseJobOperation.MULTIPLY:
                    originalValue *= value; break;
                case NoiseJobOperation.SUBTRACT:
                    originalValue -= value; break;
            }

            targetArray[index] = originalValue;
        }
    }
}
