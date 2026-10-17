using DCL.Landscape.Config;
using System;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape.NoiseGeneration
{
    public interface INoiseGenerator : IDisposable
    {
        /// <summary>
        /// This allocates the required memory and creates a noise result, unless the result was already generated, a cached result will be returned
        /// </summary>
        /// <returns></returns>
        JobHandle Schedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount = 32);

        NativeArray<float> GetResult(NoiseDataPointer noiseDataPointer);

        bool IsRecursive(NoiseDataBase otherNoiseData);
    }
}
