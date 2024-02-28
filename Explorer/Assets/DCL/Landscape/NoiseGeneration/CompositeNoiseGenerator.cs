using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.NoiseGeneration
{
    public class CompositeNoiseGenerator : BaseNoiseGenerator
    {
        private readonly CompositeNoiseData compositeNoiseData;
        private readonly NoiseGeneratorCache generatorCache;
        private readonly uint baseSeed;
        private NoiseGenerator mainJob;

        public CompositeNoiseGenerator(CompositeNoiseData compositeNoiseData, uint baseSeed, uint variantSeed, NoiseGeneratorCache generatorCache) :
            base(compositeNoiseData, variantSeed, baseSeed)
        {
            this.compositeNoiseData = compositeNoiseData;
            this.baseSeed = baseSeed;
            this.generatorCache = generatorCache;
        }

        protected override JobHandle OnSchedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount)
        {
            if (compositeNoiseData == null) return default(JobHandle);
            compositeNoiseData.settings.ValidateValues();

            NativeArray<float> targetNativeArray = noiseResultDictionary[noiseDataPointer];

            var noiseJob = new NoiseJob(targetNativeArray,
                in offsets,
                noiseDataPointer.size,
                noiseDataPointer.size,
                in noiseData.settings, maxHeight,
                new float2(noiseDataPointer.offsetX, noiseDataPointer.offsetZ),
                NoiseJobOperation.SET);

            JobHandle jobHandle = noiseJob.Schedule(noiseDataPointer.size * noiseDataPointer.size, batchCount, parentJobHandle);

            foreach (var op in compositeNoiseData.operations)
            {
                if (op.disable) continue;

                if (compositeNoiseData == op.noise)
                    continue;

                if (op.noise is not INoiseDataFactory noiseDataFactory) continue;

                INoiseGenerator generator = generatorCache.GetGeneratorFor(noiseDataFactory, baseSeed);

                if (generator.IsRecursive(compositeNoiseData))
                    continue;

                // Schedule original Noise
                JobHandle operationHandle = generator.Schedule(noiseDataPointer, jobHandle, batchCount);
                jobHandle = JobHandle.CombineDependencies(jobHandle, operationHandle);

                // Combine
                NativeArray<float> noiseToCompose = generator.GetResult(noiseDataPointer);
                var composeOperation = new NoiseComposeJob(ref targetNativeArray, noiseToCompose, op.operation);
                JobHandle composeOperationHandle = composeOperation.Schedule(noiseToCompose.Length, 32, jobHandle);
                jobHandle = JobHandle.CombineDependencies(jobHandle, composeOperationHandle);
            }

            foreach (var op in compositeNoiseData.simpleOperations)
            {
                if (op.disable) continue;
                var simpleOperationJob = new NoiseSimpleOperation(GetResult(noiseDataPointer), op.value, op.operation);
                JobHandle simpleOperationHandle = simpleOperationJob.Schedule(GetResult(noiseDataPointer).Length, 32, jobHandle);
                jobHandle = JobHandle.CombineDependencies(jobHandle, simpleOperationHandle);
            }

            var cutoffJob = new NoiseCutOffJob(GetResult(noiseDataPointer), compositeNoiseData.finalCutOff);
            JobHandle cutoffJobHandle = cutoffJob.Schedule(GetResult(noiseDataPointer).Length, 32, jobHandle);
            jobHandle = JobHandle.CombineDependencies(jobHandle, cutoffJobHandle);

            return jobHandle;
        }

        public override bool IsRecursive(NoiseDataBase otherNoiseData) =>
            this.compositeNoiseData.operations.Any(operation => operation.noise == otherNoiseData);
    }
}
