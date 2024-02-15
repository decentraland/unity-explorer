using DCL.Landscape.Config;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.NoiseGeneration
{
    public abstract class BaseNoiseGenerator : INoiseGenerator
    {
        internal NativeHashMap<NoiseDataPointer, NativeArray<float>> noiseResultDictionary;
        private NativeHashMap<NoiseDataPointer, JobHandle> jobHandleDictionary;

        internal readonly NoiseData noiseData;
        internal readonly float maxHeight;
        internal NativeArray<float2> offsets;

        protected BaseNoiseGenerator(NoiseData noiseData, uint variantSeed, uint baseSeed)
        {
            this.noiseData = noiseData;
            NoiseSettings noiseSettings = noiseData.settings;
            offsets = new NativeArray<float2>(noiseData.settings.octaves, Allocator.Persistent);
            var random = new Random(baseSeed + noiseData.settings.seed + variantSeed);
            maxHeight = Noise.CalculateOctaves(ref random, ref noiseSettings, ref offsets);

            noiseResultDictionary = new NativeHashMap<NoiseDataPointer, NativeArray<float>>(5, Allocator.Persistent);
            jobHandleDictionary = new NativeHashMap<NoiseDataPointer, JobHandle>(5, Allocator.Persistent);
        }

        private void CheckCache(NoiseDataPointer pointerKey)
        {
            if (!noiseResultDictionary.ContainsKey(pointerKey))
                noiseResultDictionary.Add(pointerKey, new NativeArray<float>(pointerKey.size * pointerKey.size, Allocator.Persistent));
        }

        public JobHandle Schedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount = 32)
        {
            CheckCache(noiseDataPointer);

            if (jobHandleDictionary.ContainsKey(noiseDataPointer))
                return jobHandleDictionary[noiseDataPointer];

            JobHandle resultHandle = OnSchedule(noiseDataPointer, parentJobHandle, batchCount);

            jobHandleDictionary.Add(noiseDataPointer, resultHandle);
            return resultHandle;
        }


        protected abstract JobHandle OnSchedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount);

        public NativeArray<float> GetResult(NoiseDataPointer noiseDataPointer) =>
            noiseResultDictionary[noiseDataPointer];

        public abstract bool IsRecursive(NoiseDataBase otherNoiseData);

        public virtual void Dispose()
        {
            ClearCache();
        }

        private void ClearCache()
        {
            foreach (KVPair<NoiseDataPointer, NativeArray<float>> pair in noiseResultDictionary)
                pair.Value.Dispose();

            noiseResultDictionary.Dispose();
            jobHandleDictionary.Dispose();
            offsets.Dispose();
        }
    }
}
