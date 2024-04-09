using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;

namespace DCL.ResourcesUnloading.Tests
{
    public class ReleaseMemorySystemShould : UnitySystemTestBase<ReleaseMemorySystem>
    {
        private ReleaseMemorySystem releaseMemorySystem;

        // Subs
        private IMemoryUsageProvider memoryBudgetProvider;
        private ICacheCleaner cacheCleaner;


        public void SetUp()
        {
            memoryBudgetProvider = Substitute.For<IMemoryUsageProvider>();
            cacheCleaner = Substitute.For<ICacheCleaner>();

            releaseMemorySystem = new ReleaseMemorySystem(world, cacheCleaner, memoryBudgetProvider);
        }




        public void UnloadCacheWhenMemoryUsageIsNotNormal(MemoryUsageStatus memoryUsageStatus, int callsAmount)
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(memoryUsageStatus);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            cacheCleaner.Received(callsAmount).UnloadCache();
        }
    }
}
