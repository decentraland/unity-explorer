using DCL.Optimization.Memory;
using NUnit.Framework;
using UnityEngine;

namespace Utility.Tests
{
    public class DynamicSlabAllocatorShould
    {
        [Test]
        public void DynamicAllocateAndWrite()
        {
            const int SIZE = 128 * 1024;
            using var a = new DynamicSlabAllocator(SIZE, 2);

            var item = a.Allocate();
            var span = item.AsSpan();
            Assert.AreEqual(SIZE, span.Length);

            for (int i = 0; i < SIZE; i++)
                span[i] = (byte)Random.Range(0, byte.MaxValue);
        }

        [Test]
        public void Release()
        {
            const int SIZE = 128 * 1024;
            using var a = new DynamicSlabAllocator(SIZE, 2);

            int count = Random.Range(10, 100);
            SlabItem[] items = new SlabItem[count];

            for (int i = 0; i < count; i++)
                items[i] = a.Allocate();

            for (int i = 0; i < count; i++)
                a.Release(items[i]);

            var info = a.Info;
            Assert.AreEqual(0, info.ChunksInUseCount);
            Assert.AreEqual(count, info.AllocatedTimes);
            Assert.AreEqual(count, info.ReturnedTimes);
        }
    }
}
