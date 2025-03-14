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
            var chain = new MemoryChain(a);

            chain.AppendData(span);

            var buffer = new byte[SIZE];
            using var stream = chain.ToStream();
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

            var stream = chain.ToStream();

            var buffer = new byte[stream.Length];

            stream.Read(buffer);

            CollectionAssert.AreEqual(new Span<byte>(randomData, size).ToArray(), buffer);
            UnsafeUtility.Free(randomData, Allocator.Persistent);
        }

        [Test]
        public unsafe void CopyDataWithIterator()
        {
            using var a = new ThreadSafeSlabAllocator<DynamicSlabAllocator>(
                new DynamicSlabAllocator(SIZE, 2)
            );

            int size = 2 * 1024 * Random.Range(800, 1024);
            void* data = NewRandomData(size);
            var span = new Span<byte>(data, size);
            using var chain = new MemoryChain(a);
            chain.AppendData(span);

            var second = new MemoryChain(a);
            using var iterator = chain.AsMemoryIterator();

            while (iterator.MoveNext())
                second.AppendData(iterator.Current.Span);

            using var stream = second.ToStream();
            var buffer = new byte[stream.Length];
            stream.Read(buffer);

            CollectionAssert.AreEqual(new Span<byte>(data, size).ToArray(), buffer);
            UnsafeUtility.Free(data, Allocator.Persistent);
        }

        [Test]
        public unsafe void MemoryChainRelease()
        {
            const int SIZE = 128 * 1024;
            using var a = new ThreadSafeSlabAllocator<DynamicSlabAllocator>(new DynamicSlabAllocator(SIZE, 2));

            int count = Random.Range(10, 100);
            var memoryChain = new MemoryChain(a);

            for (int i = 0; i < count; i++)
            {
                int size = Random.Range(10000, 50000);
                void* data = MemoryChainShould.NewRandomData(size);
                memoryChain.AppendData(data, size);
                UnsafeUtility.Free(data, Allocator.Persistent);
            }

            var stream = memoryChain.ToStream();

            stream.Dispose();

            var info = a.Info;

            Assert.AreEqual(0, info.ChunksInUseCount);
            Assert.AreEqual(info.AllocatedTimes, info.ReturnedTimes);
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
