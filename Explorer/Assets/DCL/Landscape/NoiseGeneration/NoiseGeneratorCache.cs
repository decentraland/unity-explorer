using DCL.Landscape.Config;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Assertions;

namespace DCL.Landscape.NoiseGeneration
{
    public class NoiseGeneratorCache : IDisposable
    {
        private readonly Dictionary<INoiseDataFactory, INoiseGenerator> cachedGenerators = new ();

        public static  NativeArray<float> biggerArray;
        public static  List<NativeArray<float>> smallerQueue;
        public static  int availableNativeArray;

        public NoiseGeneratorCache()
        {
            biggerArray = new NativeArray<float>(513 * 513, Allocator.Persistent);
            smallerQueue = new List<NativeArray<float>>();
            for (int i = 0; i < 20; i++)
                smallerQueue.Add(new NativeArray<float>(512 * 512, Allocator.Persistent));
            availableNativeArray = 0;
        }

        public INoiseGenerator GetGeneratorFor(INoiseDataFactory noiseData, uint baseSeed)
        {
            Assert.IsNotNull(noiseData, "Noise data is null, check the terrain generation data");

            if (cachedGenerators.TryGetValue(noiseData, out INoiseGenerator noiseGen))
                return noiseGen;

            INoiseGenerator generator = noiseData.GetGenerator(baseSeed, 0, this);
            cachedGenerators.Add(noiseData, generator);

            return cachedGenerators[noiseData];
        }

        public void ResetPool()
        {
            availableNativeArray = 0;
        }
        
        public void Dispose()
        {
            foreach (KeyValuePair<INoiseDataFactory, INoiseGenerator> cachedGenerator in cachedGenerators)
                cachedGenerator.Value.Dispose();

            biggerArray.Dispose();
            foreach (var nativeArray in smallerQueue)
            {
                nativeArray.Dispose();
            }
        }
    }
}
