using DCL.Optimization.PerformanceBudgeting;
using NUnit.Framework;
using System;

namespace DCL.Optimization.Tests
{
    [TestFixture]
    public class ConcurrentLoadingBudgetProviderShould
    {
        [Test]
        public void SpendBudget()
        {
            // Arrange
            var initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingPerformanceBudget(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

        [Test]
        public void RefillBudget()
        {
            // Arrange
            var initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingPerformanceBudget(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());

            // Act
            budgetProvider.ReleaseBudget();

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
        }

        [Test]
        public void BudgetOverflowThrown()
        {
            // Arrange
            var initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingPerformanceBudget(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());

            // Act
            budgetProvider.ReleaseBudget();
            Assert.Throws<Exception>(() => budgetProvider.ReleaseBudget());
        }
    }
}
