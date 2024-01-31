using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DCL.Landscape.NoiseGeneration
{
    public class CompositeNoiseGenerator : INoiseGenerator
    {
        private NativeArray<float> noiseResults;
        private readonly CompositeNoiseData compositeNoiseData;
        private readonly uint baseSeed;
        private readonly uint variantSeed;
        private readonly NoiseGeneratorCache generatorCache;

        private int sizeOfLastCache = -1;
        private NoiseGenerator mainJob;

        public CompositeNoiseGenerator(CompositeNoiseData compositeNoiseData, uint baseSeed, uint variantSeed, NoiseGeneratorCache generatorCache)
        {
            this.compositeNoiseData = compositeNoiseData;
            this.baseSeed = baseSeed;
            this.variantSeed = variantSeed;
            this.generatorCache = generatorCache;
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
            var tempNoiseGenerator = new NoiseGenerator(compositeNoiseData, baseSeed, variantSeed);
            JobHandle jobHandle = tempNoiseGenerator.Schedule(size, offsetX, offsetZ, batchCount);
            jobHandle.Complete();

            foreach (var op in compositeNoiseData.operations)
            {
                if (op.disable) continue;

                if (compositeNoiseData == op.noise)
                    continue;

                if (op.noise is not INoiseDataFactory noiseDataFactory) continue;

                INoiseGenerator generator = generatorCache.GetGeneratorFor(noiseDataFactory, baseSeed);

                if (generator.IsRecursive(compositeNoiseData))
                    continue;

                jobHandle = generator.Compose(ref tempNoiseGenerator.GetResult(), op.operation, size, offsetX, offsetZ, batchCount);
                jobHandle.Complete();
            }

            foreach (var op in compositeNoiseData.simpleOperations)
            {
                if (op.disable) continue;
                var noiseSimpleOp = new NoiseSimpleOperation(ref tempNoiseGenerator.GetResult(), op.value, op.operation);
                var h = noiseSimpleOp.Schedule(tempNoiseGenerator.GetResult().Length, 32);
                h.Complete();
            }

            var cutoffJob = new NoiseCutOffJob(ref tempNoiseGenerator.GetResult(), compositeNoiseData.finalCutOff);
            var handle = cutoffJob.Schedule(tempNoiseGenerator.GetResult().Length, 32);
            handle.Complete();

            var composeJob = new NoiseComposeJob(ref targetArray, in tempNoiseGenerator.GetResult(), operation);
            var cHandle = composeJob.Schedule(tempNoiseGenerator.GetResult().Length, 32);
            cHandle.Complete();

            tempNoiseGenerator.Dispose();
            return jobHandle;
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

        public ref NativeArray<float> GetResult() =>
            ref noiseResults;

        public bool IsRecursive(NoiseDataBase otherNoiseData) =>
            this.compositeNoiseData.operations.Any(operation => operation.noise == otherNoiseData);

        public void Dispose()
        {
            noiseResults.Dispose();
        }
    }
}
