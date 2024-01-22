using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class NoiseGenerator : INoiseGenerator
    {
        private readonly NoiseData noiseData;
        private readonly float maxHeight;
        private NativeArray<float2> offsets;
        private NativeArray<float> noiseResults;
        private int sizeOfLastCache = -1;
        private bool isDisposed = false;

        public NoiseGenerator(NoiseData noiseData, uint baseSeed)
        {
            this.noiseData = noiseData;
            var noiseSettings = noiseData.settings;
            offsets = new NativeArray<float2>(noiseData.settings.octaves, Allocator.Persistent);
            var random = new Random(baseSeed + noiseData.settings.seed);
            maxHeight = Noise.CalculateOctaves(ref random, ref noiseSettings, ref offsets);
        }

        public JobHandle Schedule(int size, int offsetX, int offsetZ, int batchCount = 32)
        {
            if (isDisposed) throw new Exception("Did you dispose this generator before using it?");
            if (noiseData == null) return default(JobHandle);
            CheckCache(size);

            var noiseJob = new NoiseJob(ref noiseResults, in offsets, size, size, in noiseData.settings, maxHeight, new float2(offsetX, offsetZ), NoiseJobOperation.SET);
            return noiseJob.Schedule(size * size, batchCount);
        }

        public JobHandle Compose(ref NativeArray<float> result, NoiseJobOperation operation, int size, int offsetX, int offsetZ, int batchCount = 32)
        {
            var noiseJob = new NoiseJob(ref result, in offsets, size, size, in noiseData.settings, maxHeight, new float2(offsetX, offsetZ), operation);
            return noiseJob.Schedule(size * size, batchCount);
        }

        private void CheckCache(int size)
        {
            if (sizeOfLastCache == size) return;

            if (sizeOfLastCache > 0)
                noiseResults.Dispose();

            sizeOfLastCache = size;
            noiseResults = new NativeArray<float>(size * size, Allocator.Persistent);
        }

        public float GetValue(int index) =>
            noiseResults[index];

        public NativeArray<float> GetResultCopy() =>
            noiseResults;

        public void Dispose()
        {
            isDisposed = true;
            offsets.Dispose();
            noiseResults.Dispose();
        }
    }
}
