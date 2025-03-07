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
    }
}
