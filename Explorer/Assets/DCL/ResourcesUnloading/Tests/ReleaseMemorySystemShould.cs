using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.SceneLifeCycle.IncreasingRadius;
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

            unloadStrategyHandler = new UnloadStrategyHandler(cacheCleaner);
            unloadStrategyHandler.unloadStrategies = unloadStrategies;

            ISystemMemoryCap systemMemoryCap = Substitute.For<ISystemMemoryCap>();
            var sceneLoadingLimit = new SceneLoadingLimit(systemMemoryCap);

            releaseMemorySystem = new ReleaseMemorySystem(world, memoryBudgetProvider, unloadStrategyHandler, sceneLoadingLimit);
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 1)]
        public void UnloadCacheWhenMemoryUsageIsNotNormal(bool isMemoryNormal, int callsAmount)
        {
            // Act
            memoryBudgetProvider.IsMemoryNormal().Returns(isMemoryNormal);
            releaseMemorySystem.Update(0);

            // Assert
            Assert.AreEqual(callsAmount, standardStrategy.strategyRunCount);
        }

        [Test]
        public void ResetUnloadStrategyIndexWhenMemoryUsageIsNormal()
        {
            // Arrange
            memoryBudgetProvider.IsMemoryNormal().Returns(true);

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
            memoryBudgetProvider.IsMemoryNormal().Returns(true);
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
            memoryBudgetProvider.IsMemoryNormal().Returns(false);
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
            memoryBudgetProvider.IsMemoryNormal().Returns(false);
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
