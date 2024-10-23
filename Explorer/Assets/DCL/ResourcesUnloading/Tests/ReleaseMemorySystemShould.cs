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
        public class MockUnloadStrategy : UnloadStrategy
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

        private UnloadStrategy[] unloadStrategies;

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
            Assert.AreEqual(standardStrategy.strategyRunCount, callsAmount);
        }

        [Test]
        public void ResetUnloadStrategyIndexWhenMemoryUsageIsNormal()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 1);
            
            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 2);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 1);


            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.currentFailureCount, 0);
            Assert.AreEqual(aggresiveStrategy.currentFailureCount, 0);
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
            Assert.AreEqual(standardStrategy.strategyRunCount, 1);

            // Act
            releaseMemorySystem.Update(0);
            
            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 2);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 1);
        }

        [Test]
        public void SkipAggressiveStrategyIfPreviousDidNotFail()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);
            standardStrategy.currentFailureCount = 5;

            // Act

            for (var i = 0; i < 5; i++)
            {
                releaseMemorySystem.Update(0);
            }


            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 1);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 0);


            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 2);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 1);
        }

        
    }

}
