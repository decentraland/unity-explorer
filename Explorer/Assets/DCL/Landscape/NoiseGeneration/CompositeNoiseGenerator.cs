using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Landscape
{
    public class CompositeNoiseGenerator : INoiseGenerator
    {
        private NativeArray<float> noiseResults;
        private readonly CompositeNoiseData compositeNoiseData;
        private readonly uint baseSeed;
        private Dictionary<INoiseDataFactory, INoiseGenerator> noiseGenerators = new();

        private int sizeOfLastCache = -1;
        private NoiseGenerator mainJob;

        public CompositeNoiseGenerator(CompositeNoiseData compositeNoiseData, uint baseSeed)
        {
            this.compositeNoiseData = compositeNoiseData;
            this.baseSeed = baseSeed;
        }

        public JobHandle Schedule(int size, int offsetX, int offsetZ, int batchCount = 32)
        {
            if (compositeNoiseData == null) return default(JobHandle);
            CheckCache(size);
            compositeNoiseData.settings.ValidateValues();

            return Execute(ref noiseResults, NoiseJobOperation.SET, size, offsetX, offsetZ, batchCount);
        }

        public JobHandle Compose(ref NativeArray<float> result, NoiseJobOperation operation, int size, int offsetX, int offsetZ,
            int batchCount = 32)
        {
            if (compositeNoiseData == null) return default(JobHandle);
            compositeNoiseData.settings.ValidateValues();
            return Execute(ref result, operation, size, offsetX, offsetZ, batchCount);
        }

        private JobHandle Execute(ref NativeArray<float> targetArray, NoiseJobOperation operation, int size, int offsetX, int offsetZ,
            int batchCount = 32)
        {
            mainJob = new NoiseGenerator(compositeNoiseData, baseSeed);
            JobHandle jobHandle = mainJob.Compose(ref targetArray, operation, size, offsetX, offsetZ, batchCount);
            jobHandle.Complete();

            foreach (NoiseData noise in compositeNoiseData.add)
            {
                if (noise is INoiseDataFactory noiseDataFactory)
                {
                    INoiseGenerator generator = GetGenerator(noiseDataFactory);
                    jobHandle = generator.Compose(ref targetArray, NoiseJobOperation.ADD, size, offsetX, offsetZ, batchCount);
                    jobHandle.Complete();
                }
            }

            foreach (NoiseData noise in compositeNoiseData.multiply)
            {
                if (noise is INoiseDataFactory noiseDataFactory)
                {
                    INoiseGenerator generator = GetGenerator(noiseDataFactory);
                    jobHandle = generator.Compose(ref targetArray, NoiseJobOperation.MULTIPLY, size, offsetX, offsetZ, batchCount);
                    jobHandle.Complete();
                }
            }

            foreach (NoiseData noise in compositeNoiseData.subtract)
            {
                if (noise is INoiseDataFactory noiseDataFactory)
                {
                    INoiseGenerator generator = GetGenerator(noiseDataFactory);
                    jobHandle = generator.Compose(ref targetArray, NoiseJobOperation.SUBTRACT, size, offsetX, offsetZ, batchCount);
                    jobHandle.Complete();
                }
            }

            return jobHandle;
        }

        private INoiseGenerator GetGenerator(INoiseDataFactory noiseDataFactory)
        {
            if (noiseGenerators.TryGetValue(noiseDataFactory, out var result))
                return result;

            var generator = noiseDataFactory.GetGenerator(baseSeed);
            noiseGenerators.Add(noiseDataFactory, generator);
            return generator;
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
            foreach (KeyValuePair<INoiseDataFactory,INoiseGenerator> noiseGenerator in noiseGenerators)
                noiseGenerator.Value.Dispose();

            noiseResults.Dispose();
            mainJob.Dispose();
        }
    }
}
