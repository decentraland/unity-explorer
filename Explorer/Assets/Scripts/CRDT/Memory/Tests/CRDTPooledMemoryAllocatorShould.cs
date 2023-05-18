using Instrumentation;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Reflection;

namespace CRDT.Memory.Tests
{
    [TestFixture]
    public class CRDTPooledMemoryAllocatorShould
    {
        private const int TOLERATED_INSTANCE_OVERHEAD = 100;

        private static readonly MethodInfo SELECT_BUCKET_INDEX = Type.GetType("System.Buffers.Utilities").GetMethod("SelectBucketIndex", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo GET_MAX_SIZE_FOR_BUCKET = Type.GetType("System.Buffers.Utilities").GetMethod("GetMaxSizeForBucket", BindingFlags.NonPublic | BindingFlags.Static);

        [SetUp]
        public void SetUp()
        {
            allocator = CRDTPooledMemoryAllocator.Create();
        }

        private CRDTPooledMemoryAllocator allocator;

        [Test]
        public void AllocateOnColdRun([Values(128, 1024, 8096, 256 * 1024, 512 * 1024, 1024 * 1024)] int size)
        {
            var poolAllocationSize = (int)GET_MAX_SIZE_FOR_BUCKET.Invoke(null, new object[] { (int)SELECT_BUCKET_INDEX.Invoke(null, new object[] { size }) });

            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(
                () => allocator.GetMemoryBuffer(size),
                value => { Assert.AreEqual(poolAllocationSize, value, TOLERATED_INSTANCE_OVERHEAD); });
        }

        [Test]
        public void AllocateIfSizeExceedsTheMaximum()
        {
            int oversize = CRDTPooledMemoryAllocator.POOL_MAX_ARRAY_LENGTH + 1;

            var data = new byte[oversize];
            var random = new Random();
            random.NextBytes(data);
            IMemoryOwner<byte> allocation = allocator.GetMemoryBuffer(data);

            for (var i = 0; i < 100; i++)
            {
                MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(
                    () =>
                    {
                        allocation.Dispose();
                        allocation = allocator.GetMemoryBuffer(data);
                    },
                    value => Assert.AreEqual(oversize, value, TOLERATED_INSTANCE_OVERHEAD));
            }
        }

        [Test]
        public void NotAllocateOnHotRun([Values(128, 1024, 8096, 256 * 1024, 512 * 1024, 1024 * 1024)] int size)
        {
            var data = new byte[size];
            var random = new Random();
            random.NextBytes(data);
            IMemoryOwner<byte> allocation = allocator.GetMemoryBuffer(data);

            for (var i = 0; i < 100; i++)
            {
                MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(
                    () =>
                    {
                        allocation.Dispose();
                        allocation = allocator.GetMemoryBuffer(data);
                    },
                    Assert.Zero);
            }
        }
    }
}
