using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;

namespace DCL.ResourcesUnloading.Tests
{
    public class ReleaseMemorySystemShould : UnitySystemTestBase<ReleaseMemorySystem>
    {
        /*
        private ReleaseMemorySystem releaseMemorySystem;

        // Subs
        private IMemoryUsageProvider memoryBudgetProvider;
        private ICacheCleaner cacheCleaner;

        private IUnloadStrategy[] unloadStrategies;
        private int frameFailThreshold;

        private IUnloadStrategy standardStrategy;
        private IUnloadStrategy aggresiveStrategy;

        [SetUp]
        public void SetUp()
        {
            memoryBudgetProvider = Substitute.For<IMemoryUsageProvider>();
            cacheCleaner = Substitute.For<ICacheCleaner>();

            unloadStrategies = new[]
            {
                standardStrategy = Substitute.For<IUnloadStrategy>(),
                aggresiveStrategy = Substitute.For<IUnloadStrategy>()
            };

            frameFailThreshold = 2;

            releaseMemorySystem = new ReleaseMemorySystem(world, cacheCleaner, memoryBudgetProvider, unloadStrategies,
                frameFailThreshold);
        }

        [TestCase(MemoryUsageStatus.NORMAL, 0)]
        [TestCase(MemoryUsageStatus.WARNING, 1)]
        [TestCase(MemoryUsageStatus.FULL, 1)]
        public void UnloadCacheWhenMemoryUsageIsNotNormal(MemoryUsageStatus memoryUsageStatus, int callsAmount)
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(memoryUsageStatus);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            standardStrategy.Received(callsAmount).TryUnload(cacheCleaner);
        }

        [Test]
        public void ResetUnloadStrategyIndexWhenMemoryUsageIsNormal()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);

            // Act
            for (var i = 0; i < frameFailThreshold; i++)
                releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(releaseMemorySystem.currentUnloadStrategy, 1);

            // Act
            releaseMemorySystem.Update(0);
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(releaseMemorySystem.currentUnloadStrategy, 0);

            standardStrategy.Received(frameFailThreshold).TryUnload(cacheCleaner);
            aggresiveStrategy.Received(1).TryUnload(cacheCleaner);
        }

        [Test]
        public void UnloadDoesntGetCalledAgainIfRunningInStrategy()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);

            // Act
            //Run until the fail and one more
            for (var i = 0; i < frameFailThreshold + 1; i++)
                releaseMemorySystem.Update(0);

            //Simulate that it started running
            aggresiveStrategy.IsRunning.Returns(true);

            for (var i = 0; i < frameFailThreshold + 5; i++)
                releaseMemorySystem.Update(0);

            standardStrategy.Received(frameFailThreshold).TryUnload(cacheCleaner);
            aggresiveStrategy.Received(1).TryUnload(cacheCleaner);
        }
    */
    }
    
}
