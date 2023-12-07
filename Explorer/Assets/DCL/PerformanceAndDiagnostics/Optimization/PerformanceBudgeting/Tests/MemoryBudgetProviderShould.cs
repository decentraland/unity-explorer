using DCL.Profiling;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Optimization.PerformanceBudgeting.Tests
{
    internal class MemoryBudgetProviderShould
    {
        private const ulong BYTES_IN_MEGABYTE = 1024 * 1024;

        private readonly Dictionary<MemoryUsageStatus, float> memoryThreshold = new ()
        {
            { MemoryUsageStatus.Warning, 0.8f },
            { MemoryUsageStatus.Full, 0.9f },
        };

        private MemoryBudgetProvider memoryBudgetProvider;

        private IProfilingProvider profilingProvider;
        private ISystemMemory systemMemory;

        [SetUp]
        public void Setup()
        {
            profilingProvider = Substitute.For<IProfilingProvider>();
            systemMemory = Substitute.For<ISystemMemory>();

            memoryBudgetProvider = new MemoryBudgetProvider(systemMemory, profilingProvider, memoryThreshold);
        }

        [TestCase((ulong)1000, (ulong)500, MemoryUsageStatus.Normal)]
        [TestCase((ulong)1000, (ulong)810, MemoryUsageStatus.Warning)]
        [TestCase((ulong)1000, (ulong)910, MemoryUsageStatus.Full)]
        public void ReturnCorrectMemoryStatus_OnDifferentMemoryUsages(ulong systemMemoryInMB, ulong usedMemoryInMB, MemoryUsageStatus expectedUsage)
        {
            // Arrange
            systemMemory.TotalSizeInMB.Returns(systemMemoryInMB);
            profilingProvider.TotalUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);

            // Act-Assert
            Assert.That(memoryBudgetProvider.GetMemoryUsageStatus(), Is.EqualTo(expectedUsage));
        }

        [TestCase((ulong)1000, (ulong)810, true)]
        [TestCase((ulong)1000, (ulong)910, false)]
        public void CanSpendBudgetOnlyWhenMemoryIsNotFull(ulong systemMemoryInMB, ulong usedMemoryInMB, bool canSpendBudget)
        {
            // Arrange
            profilingProvider.TotalUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);
            systemMemory.TotalSizeInMB.Returns(systemMemoryInMB);

            // Act-Assert
            Assert.That(memoryBudgetProvider.TrySpendBudget(), Is.EqualTo(canSpendBudget));
        }
    }
}
