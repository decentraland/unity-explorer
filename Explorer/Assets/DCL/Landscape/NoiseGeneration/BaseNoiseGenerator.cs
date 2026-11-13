using DCL.Diagnostics;
using DCL.Landscape.Config;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.NoiseGeneration
{
    public abstract class BaseNoiseGenerator : INoiseGenerator
    {
        internal NativeHashMap<NoiseDataPointer, NativeArray<float>> noiseResultDictionary;
        private NativeHashMap<NoiseDataPointer, JobHandle> jobHandleDictionary;
        private readonly NoiseNativeArrayProvider noiseNativeArrayProvider;
        
        internal readonly NoiseData noiseData;
        internal readonly float maxHeight;
        internal NativeArray<float2> offsets;


        protected BaseNoiseGenerator(NoiseData noiseData, uint variantSeed, uint baseSeed, NoiseNativeArrayProvider noiseNativeArrayProvider)
        {
            this.noiseData = noiseData;
            this.noiseNativeArrayProvider = noiseNativeArrayProvider;
            NoiseSettings noiseSettings = noiseData.settings;
            offsets = new NativeArray<float2>(noiseData.settings.octaves, Allocator.Persistent);
            var random = new Random(baseSeed + noiseData.settings.seed + variantSeed);
            maxHeight = Noise.CalculateOctaves(ref random, ref noiseSettings, ref offsets);

            noiseResultDictionary = new NativeHashMap<NoiseDataPointer, NativeArray<float>>(5, Allocator.Persistent);
            jobHandleDictionary = new NativeHashMap<NoiseDataPointer, JobHandle>(5, Allocator.Persistent);
        }

        private NativeArray<float> GetOrCreateResult(NoiseDataPointer pointerKey)
        {
            if (noiseResultDictionary.TryGetValue(pointerKey, out NativeArray<float> result))
                return result;

            result = noiseNativeArrayProvider.GetNoiseNativeArray(pointerKey.size);
            noiseResultDictionary[pointerKey] = result;
            return result;
        }

        public JobHandle Schedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount = 32)
        {
            GetOrCreateResult(noiseDataPointer);

            if (jobHandleDictionary.TryGetValue(noiseDataPointer, out JobHandle scheduledHandle))
                return scheduledHandle;

            JobHandle resultHandle = OnSchedule(noiseDataPointer, parentJobHandle, batchCount);

            jobHandleDictionary[noiseDataPointer] = resultHandle;
            return resultHandle;
        }


        protected abstract JobHandle OnSchedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount);

        public NativeArray<float> GetResult(NoiseDataPointer noiseDataPointer)
        {
            if (noiseResultDictionary.TryGetValue(noiseDataPointer, out NativeArray<float> result))
                return result;

            ReportHub.LogError(ReportCategory.LANDSCAPE, $"Noise result requested before scheduling (size {noiseDataPointer.size}, offset {noiseDataPointer.offsetX},{noiseDataPointer.offsetZ})");
            return GetOrCreateResult(noiseDataPointer);
        }

        public abstract bool IsRecursive(NoiseDataBase otherNoiseData);

        public virtual void Dispose()
        {
            ClearCache();
        }

        private void ClearCache()
        {
            noiseResultDictionary.Dispose();
            jobHandleDictionary.Dispose();
            offsets.Dispose();
        }
    }
}
