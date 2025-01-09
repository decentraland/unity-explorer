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
        public class MockUnloadStrategy : UnloadStrategyBase
        {
            public int strategyRunCount;

            public override void RunStrategy()
            {
                strategyRunCount++;
            }

            public MockUnloadStrategy(int failureThreshold) : base(failureThreshold)
            {
            }
        }
        
        
        private ReleaseMemorySystem releaseMemorySystem;

        // Subs
        private IMemoryUsageProvider memoryBudgetProvider;
        private ICacheCleaner cacheCleaner;

        private UnloadStrategyBase[] unloadStrategies;

        private MockUnloadStrategy standardStrategy;
        private MockUnloadStrategy aggresiveStrategy;

        private UnloadStrategyHandler unloadStrategyHandler;

        [SetUp]
        public void SetUp()
        {
            memoryBudgetProvider = Substitute.For<IMemoryUsageProvider>();
            cacheCleaner = Substitute.For<ICacheCleaner>();
            standardStrategy = new MockUnloadStrategy(1);
            aggresiveStrategy = new MockUnloadStrategy(1);

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
            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(memoryUsageStatus);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(callsAmount, standardStrategy.strategyRunCount);
        }

        [Test]
        public void ResetUnloadStrategyIndexWhenMemoryUsageIsNormal()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(1, standardStrategy.strategyRunCount);
            
            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(2, standardStrategy.strategyRunCount);
            Assert.AreEqual(1, aggresiveStrategy.strategyRunCount);


            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(0, standardStrategy.currentFailureCount);
            Assert.AreEqual(0, aggresiveStrategy.currentFailureCount);
            Assert.IsFalse(standardStrategy.FaillingOverThreshold());
        }
        
        [Test]
        public void IncreaseTierAggresiveness()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);
            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(1, standardStrategy.strategyRunCount);

            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(2, standardStrategy.strategyRunCount);
            Assert.AreEqual(1, aggresiveStrategy.strategyRunCount);
        }

        [Test]
        public void SkipAggressiveStrategyIfPreviousDidNotFail()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);
            standardStrategy.failureThreshold = 5;

            // Act

            for (var i = 0; i < 5; i++)
                releaseMemorySystem.Update(0);


            // Assert
            Assert.AreEqual(5, standardStrategy.strategyRunCount);
            Assert.AreEqual(0, aggresiveStrategy.strategyRunCount);
        }

        
    }

}
