using DCL.Optimization.Memory;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Utility.Tests
{
    public class MemoryChainShould
    {
        private const int SIZE = 128 * 1024;

        [Test]
        public unsafe void PassDataSingleSlab()
        {
            using var a = new ThreadSafeSlabAllocator<DynamicSlabAllocator>(
                new DynamicSlabAllocator(SIZE, 2)
            );

            void* data = NewRandomData(SIZE);
            Span<byte> span = new Span<byte>(data, SIZE);
            using var chain = new MemoryChain(a);

            chain.AppendData(span);

            var buffer = new byte[SIZE];
            using var stream = chain.AsStream();
            stream.Read(buffer);

            CollectionAssert.AreEqual(span.ToArray(), buffer);
            UnsafeUtility.Free(data, Allocator.Persistent);
        }

        [Test]
        public unsafe void PassData()
        {
            using var a = new ThreadSafeSlabAllocator<DynamicSlabAllocator>(
                new DynamicSlabAllocator(SIZE, 2)
            );

            using var chain = new MemoryChain(a);

            int size = Mathf.RoundToInt(128 * 1024 * Random.Range(1, 5f));
            size += size % sizeof(int);
            void* randomData = NewRandomData(size);

            chain.AppendData(randomData, size);

            var stream = chain.AsStream();

            var buffer = new byte[stream.Length];

            stream.Read(buffer);

            CollectionAssert.AreEqual(new Span<byte>(randomData, size).ToArray(), buffer);
            UnsafeUtility.Free(randomData, Allocator.Persistent);
        }

        private static unsafe void* NewRandomData(int size)
        {
            void* randomData = UnsafeUtility.Malloc(size, 64, Allocator.Persistent);

            for (int i = 0; i < size - sizeof(int); i += sizeof(int))
            {
                int number = Random.Range(0, int.MaxValue);
                UnsafeUtility.MemCpy(randomData, &number, sizeof(int));
            }

            return randomData;
        }
    }
}
