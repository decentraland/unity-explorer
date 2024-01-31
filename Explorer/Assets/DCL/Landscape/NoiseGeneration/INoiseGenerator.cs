using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape.NoiseGeneration
{
    public interface INoiseGenerator : IDisposable
    {
        /// <summary>
        /// This allocates the required memory to create a noise result
        /// </summary>
        /// <returns></returns>
        JobHandle Schedule(int size, int offsetX, int offsetZ, int batchCount = 32);

        /// <summary>
        /// This does NOT allocate memory, instead uses an external native array which is NOT going to be disposed if this class is disposed
        /// </summary>
        /// <returns></returns>
        JobHandle Compose(ref NativeArray<float> result, NoiseJobOperation operation, int size, int offsetX, int offsetZ, int batchCount = 32);

        float GetValue(int index);

        ref NativeArray<float> GetResult();

        bool IsRecursive(NoiseDataBase otherNoiseData);
    }
}
