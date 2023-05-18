using NUnit.Framework;
using System;
using System.Buffers;
using UnityEngine.Profiling;

namespace CRDT.Memory.Tests
{
    public class MemoryAllocatorShould
    {
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void OriginalMemorySlicerAllocating(int arraySize)
        {
            var originalMemorySlicer = CRDTOriginalMemorySlicer.Create();
            var data = new byte[arraySize];
            var random = new Random();
            random.NextBytes(data);
            IMemoryOwner<byte> allocation = originalMemorySlicer.GetMemoryBuffer(data);

            for (var i = 0; i < 100; i++)
            {
                Profiler.BeginSample($"Debug: CRDT Memory Slicer {arraySize}");
                allocation.Dispose();
                allocation = originalMemorySlicer.GetMemoryBuffer(data);
                Profiler.EndSample();
            }
        }
    }
}
