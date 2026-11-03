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
            { MemoryUsageStatus.Abundance, 0.6f },
            { MemoryUsageStatus.Warning, 0.8f },
            { MemoryUsageStatus.Full, 0.9f },
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

        [TestCase(1000, 500, true, false, true)]
        [TestCase(1000, 810, false, false, false)]
        [TestCase(1000, 910, false, true, false)]
        public void ReturnCorrectMemoryStatus_OnDifferentMemoryUsages(long systemMemoryInMB, long usedMemoryInMB, bool expectedUsage, bool isFull, bool isAbundance)
        {
            // Arrange
            profiler.SystemUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);
            systemMemoryCap.MemoryCapInMB.Returns((int)systemMemoryInMB);

            // Act-Assert
            Assert.That(memoryBudget.IsMemoryNormal(), Is.EqualTo(expectedUsage));
            Assert.That(memoryBudget.IsMemoryFull(), Is.EqualTo(isFull));
            Assert.That(memoryBudget.IsInAbundance(), Is.EqualTo(isAbundance));
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

        [Test]
        public void TreatLowMemoryAsNormalWithoutAbundanceThreshold()
        {
            // Arrange
            var thresholdsWithoutAbundance = new Dictionary<MemoryUsageStatus, float>
            {
                { MemoryUsageStatus.Warning, 0.65f },
                { MemoryUsageStatus.Full, 0.75f },
            };

            var budget = new MemoryBudget(systemMemoryCap, profiler, thresholdsWithoutAbundance);
            profiler.SystemUsedMemoryInBytes.Returns(100 * BYTES_IN_MEGABYTE);
            systemMemoryCap.MemoryCapInMB.Returns(1000);

            // Act-Assert
            Assert.That(budget.TrySpendBudget(), Is.True);
            Assert.That(budget.IsInAbundance(), Is.False);
            Assert.That(budget.IsMemoryNormal(), Is.True);
        }
    }
}
