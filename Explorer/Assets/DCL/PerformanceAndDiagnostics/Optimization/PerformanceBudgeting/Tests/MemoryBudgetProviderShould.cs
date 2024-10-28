using DCL.Profiling;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Optimization.PerformanceBudgeting.Tests
{
    internal class MemoryBudgetProviderShould
    {
        private const long BYTES_IN_MEGABYTE = 1024 * 1024;

        private readonly Dictionary<MemoryUsageStatus, float> memoryThreshold = new ()
        {
            { MemoryUsageStatus.WARNING, 0.8f },
            { MemoryUsageStatus.FULL, 0.9f },
        };

        private MemoryBudget memoryBudget;

        private IBudgetProfiler profiler;
        private ISystemMemoryCap systemMemoryCap;

        [SetUp]
        public void Setup()
        {
            profiler = Substitute.For<IBudgetProfiler>();
            systemMemoryCap = Substitute.For<ISystemMemoryCap>();

            memoryBudget = new MemoryBudget(systemMemoryCap, profiler, memoryThreshold);
        }

        [TestCase(1000, 500, MemoryUsageStatus.NORMAL)]
        [TestCase(1000, 810, MemoryUsageStatus.WARNING)]
        [TestCase(1000, 910, MemoryUsageStatus.FULL)]
        public void ReturnCorrectMemoryStatus_OnDifferentMemoryUsages(long systemMemoryInMB, long usedMemoryInMB, MemoryUsageStatus expectedUsage)
        {
            // Arrange
            profiler.SystemUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);
            systemMemoryCap.MemoryCapInMB.Returns((int)systemMemoryInMB);

            // Act-Assert
            Assert.That(memoryBudget.GetMemoryUsageStatus(), Is.EqualTo(expectedUsage));
        }

        [TestCase(1000, 810, true)]
        [TestCase(1000, 910, false)]
        public void CanSpendBudgetOnlyWhenMemoryIsNotFull(long systemMemoryInMB, long usedMemoryInMB, bool canSpendBudget)
        {
            // Arrange
            profiler.SystemUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);
            systemMemoryCap.MemoryCapInMB.Returns((int)systemMemoryInMB);

            // Act-Assert
            Assert.That(memoryBudget.TrySpendBudget(), Is.EqualTo(canSpendBudget));
        }
    }
}
