using NUnit.Framework;
using System;
using Utility.Memory;
using Random = UnityEngine.Random;

namespace Utility.Tests
{
    public class SlabAllocatorShould
    {
        [Test]
        public void AllocateFailEmpty()
        {
            Assert.Throws<Exception>(() =>
            {
                using var a = new SlabAllocator(100, 0);
                a.Allocate();
            });
        }

        [Test]
        public void AllocateFail()
        {
            Assert.Throws<Exception>(() =>
            {
                using var a = new SlabAllocator(128, 16);

                for (int i = 0; i < 17; i++)
                    a.Allocate();
            });
        }

        [Test]
        public void AllocateAndWrite()
        {
            const int SIZE = 128 * 1024;
            using var a = new SlabAllocator(SIZE, 2);

            var item = a.Allocate();
            var span = item.AsSpan();
            Assert.AreEqual(SIZE, span.Length);

            for (int i = 0; i < SIZE; i++)
                span[i] = (byte)Random.Range(0, byte.MaxValue);
        }
    }
}
