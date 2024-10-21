using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Prioritization;
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

        private IUnloadStrategy[] unloadStrategies;

        private IUnloadStrategy standardStrategy;
        private IUnloadStrategy aggresiveStrategy;

        private UnloadStrategyHandler unloadStrategyHandler;

        [SetUp]
        public void SetUp()
        {
            memoryBudgetProvider = Substitute.For<IMemoryUsageProvider>();
            cacheCleaner = Substitute.For<ICacheCleaner>();
            standardStrategy = Substitute.For<IUnloadStrategy>();
            aggresiveStrategy = Substitute.For<IUnloadStrategy>();

            unloadStrategies = new[]
            {
                standardStrategy,
                aggresiveStrategy
            };


            var partitionSettings = Substitute.For<IRealmPartitionSettings>();

            unloadStrategyHandler = new UnloadStrategyHandler(partitionSettings, cacheCleaner);
            unloadStrategyHandler.unloadStrategies = unloadStrategies;

            releaseMemorySystem = new ReleaseMemorySystem(world, memoryBudgetProvider, unloadStrategyHandler);
        }

        [TestCase(MemoryUsageStatus.NORMAL, 0)]
        [TestCase(MemoryUsageStatus.WARNING, 1)]
        [TestCase(MemoryUsageStatus.FULL, 1)]
        public void UnloadCacheWhenMemoryUsageIsNotNormal(MemoryUsageStatus memoryUsageStatus, int callsAmount)
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(memoryUsageStatus);
            standardStrategy.FailedOverThreshold().Returns(true);

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
            standardStrategy.FailedOverThreshold().Returns(true);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 1);
            standardStrategy.Received(1).TryUnload(cacheCleaner);
            
            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 1);
            standardStrategy.Received(1).TryUnload(cacheCleaner);
            aggresiveStrategy.Received(1).TryUnload(cacheCleaner);


            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 0);
            standardStrategy.Received(1).ResetStrategy();
            aggresiveStrategy.Received(1).ResetStrategy();
        }
        
        [Test]
        public void IncreaseTierAggresiveness()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);
            standardStrategy.FailedOverThreshold().Returns(true);
            
            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 1);
            standardStrategy.Received(1).TryUnload(cacheCleaner);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 1);
            standardStrategy.Received(1).TryUnload(cacheCleaner);
            aggresiveStrategy.Received(1).TryUnload(cacheCleaner);
            
            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(unloadStrategyHandler.currentUnloadStrategy, 1);
            standardStrategy.Received(1).TryUnload(cacheCleaner);
            aggresiveStrategy.Received(2).TryUnload(cacheCleaner);
        }

        
    }

}
