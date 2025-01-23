using System;
using System.Collections.Generic;
using Unity.Collections;

namespace DCL.Landscape.NoiseGeneration
{
    public class NoiseNativeArrayProvider : IDisposable
    {
        private readonly Dictionary<int, Queue<NativeArray<float>>> noiseNativeArrayDictionary = new();
        private readonly List<NativeArray<float>> allNativeArrays = new();

        public void Reset()
        {
            foreach (var nativeArray in allNativeArrays)
            {
                int size = (int)Math.Sqrt(nativeArray.Length);
                if (!noiseNativeArrayDictionary.ContainsKey(size))
                    noiseNativeArrayDictionary[size] = new Queue<NativeArray<float>>();
                noiseNativeArrayDictionary[size].Enqueue(nativeArray);
            }

            allNativeArrays.Clear();
        }

        public NativeArray<float> GetNoiseNativeArray(int key)
        {
            // Check if there's a reusable array in the queue for the key
            if (noiseNativeArrayDictionary.TryGetValue(key, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            // If no reusable array, create a new one
            var newArray = new NativeArray<float>(key * key, Allocator.Persistent);
            allNativeArrays.Add(newArray);
            return newArray;
        }

        public void Dispose()
        {
            foreach (var queue in noiseNativeArrayDictionary.Values)
            {
                while (queue.Count > 0)
                {
                    var nativeArray = queue.Dequeue();
                    if (nativeArray.IsCreated)
                        nativeArray.Dispose();
                }
            }

            foreach (var nativeArray in allNativeArrays)
            {
                if (nativeArray.IsCreated)
                    nativeArray.Dispose();
            }

            noiseNativeArrayDictionary.Clear();
            allNativeArrays.Clear();
        }
    }
}