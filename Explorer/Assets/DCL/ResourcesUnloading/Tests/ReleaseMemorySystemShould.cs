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
        public class MockUnloadStrategy : UnloadStrategy
        {
            public int strategyRunCount = 0;
            public int strategyResetCount = 0;
            public MockUnloadStrategy(UnloadStrategy? previousStrategy) : base(previousStrategy)
            {
            }

            protected override void RunStrategy(ICacheCleaner cacheCleaner)
            {
                strategyRunCount++;
            }
            
            protected override void ResetStrategy()
            {
                strategyResetCount++;
            }
        }
        
        private ReleaseMemorySystem releaseMemorySystem;

        private IMemoryUsageProvider memoryBudgetProvider;
        private ICacheCleaner cacheCleaner;
        private MockUnloadStrategy aggresiveStrategy;
        private MockUnloadStrategy standardStrategy;


        [SetUp]
        public void SetUp()
        {
            memoryBudgetProvider = Substitute.For<IMemoryUsageProvider>();
            cacheCleaner = Substitute.For<ICacheCleaner>();

            standardStrategy = new MockUnloadStrategy(null);
            aggresiveStrategy = new MockUnloadStrategy(standardStrategy);
            
            releaseMemorySystem = new ReleaseMemorySystem(world, memoryBudgetProvider, aggresiveStrategy, cacheCleaner);
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
            Assert.AreEqual(standardStrategy.strategyRunCount, callsAmount);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 0);
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
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 0);
            
            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyResetCount, 1);
            Assert.AreEqual(aggresiveStrategy.strategyResetCount, 1);
        }
        
        [Test]
        public void IncreaseTierAggresiveness()
        {
            // Arrange
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.WARNING);
            standardStrategy.FAILURE_THRESHOLD = 1;
            
            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 1);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 0);

            // Act
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 2);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 1);
            
            // Act
            memoryBudgetProvider.GetMemoryUsageStatus().Returns(MemoryUsageStatus.NORMAL);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(standardStrategy.strategyRunCount, 2);
            Assert.AreEqual(aggresiveStrategy.strategyRunCount, 1);
            Assert.AreEqual(aggresiveStrategy.currentFailureCount, 0);
            Assert.AreEqual(aggresiveStrategy.currentFailureCount, 0);
            Assert.AreEqual(standardStrategy.strategyResetCount, 1);
            Assert.AreEqual(aggresiveStrategy.strategyResetCount, 1);
        }

        
    }

}
