using NUnit.Framework;
using System;
using System.Buffers;
using UnityEngine.Profiling;

namespace CRDT.Memory.Tests
{
    public class MemoryAllocatorShould
    {



        public void PoolNotAllocating(int arraySize)
        {
            var crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();

            var data = new byte[arraySize];
            var random = new Random();
            random.NextBytes(data);
            IMemoryOwner<byte> allocation = crdtPooledMemoryAllocator.GetMemoryBuffer(data);

            for (var i = 0; i < 100; i++)
            {
                Profiler.BeginSample($"Debug: CRDT Pooled Memory {arraySize}");
                allocation.Dispose();
                allocation = crdtPooledMemoryAllocator.GetMemoryBuffer(data);
                Profiler.EndSample();
            }
        }




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
