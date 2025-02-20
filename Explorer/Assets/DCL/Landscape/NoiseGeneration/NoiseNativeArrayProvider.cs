using System;
using System.Collections.Generic;
using Unity.Collections;

namespace DCL.Landscape.NoiseGeneration
{
    internal class NoiseNativeArray : IDisposable
    {
        private int currentIndexUsed;
        private readonly List<NativeArray<float>> nativeArrays = new ();
        private readonly int size;

        public NoiseNativeArray(int size)
        {
            this.size = size;
        }

        public NativeArray<float> GetNativeArray()
        {
            if (currentIndexUsed >= nativeArrays.Count)
                nativeArrays.Add(new NativeArray<float>(size * size, Allocator.Persistent));
            return nativeArrays[currentIndexUsed++];
        }

        public void Reset()
        {
            currentIndexUsed = 0;
        }

        public void Dispose()
        {
            foreach (var nativeArray in nativeArrays)
            {
                nativeArray.Dispose();
            }
        }
    }
    
    public class NoiseNativeArrayProvider : IDisposable
    {
        private readonly Dictionary<int, NoiseNativeArray> noiseNativeArrayDictionary = new();

        public void Reset()
        {
            foreach (var noiseNativeArray in noiseNativeArrayDictionary.Values)
            {
                noiseNativeArray.Reset();
            }
        }

        public NativeArray<float> GetNoiseNativeArray(int key)
        {
            if (noiseNativeArrayDictionary.TryGetValue(key, out var noiseArray))
                return noiseArray.GetNativeArray();

            var newArray = new NoiseNativeArray(key);
            noiseNativeArrayDictionary[key] = newArray;
            return newArray.GetNativeArray();
        }

        public void Dispose()
        {
            foreach (var noiseNativeArray in noiseNativeArrayDictionary.Values)
            {
                noiseNativeArray.Dispose();
            }
        }
    }
}