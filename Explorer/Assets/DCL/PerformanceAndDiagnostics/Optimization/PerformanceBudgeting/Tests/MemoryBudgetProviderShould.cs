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

        private MemoryBudget memoryBudget;

        private IProfilingProvider profilingProvider;
        private ISystemMemory systemMemory;


        public void Setup()
        {
            profilingProvider = Substitute.For<IProfilingProvider>();
            systemMemory = Substitute.For<ISystemMemory>();

            memoryBudget = new MemoryBudget(systemMemory, profilingProvider, memoryThreshold);
        }




        public void ReturnCorrectMemoryStatus_OnDifferentMemoryUsages(ulong systemMemoryInMB, ulong usedMemoryInMB, MemoryUsageStatus expectedUsage)
        {
            // Arrange
            memoryBudget.ActualSystemMemory = systemMemoryInMB;
            profilingProvider.TotalUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);

            // Act-Assert
            Assert.That(memoryBudget.GetMemoryUsageStatus(), Is.EqualTo(expectedUsage));
        }



        public void CanSpendBudgetOnlyWhenMemoryIsNotFull(ulong systemMemoryInMB, ulong usedMemoryInMB, bool canSpendBudget)
        {
            // Arrange
            profilingProvider.TotalUsedMemoryInBytes.Returns(usedMemoryInMB * BYTES_IN_MEGABYTE);
            memoryBudget.ActualSystemMemory = systemMemoryInMB;

            // Act-Assert
            Assert.That(memoryBudget.TrySpendBudget(), Is.EqualTo(canSpendBudget));
        }
    }
}
