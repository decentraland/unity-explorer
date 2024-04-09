using DCL.Optimization.PerformanceBudgeting;
using NUnit.Framework;
using System;

namespace DCL.Optimization.Tests
{

    public class ConcurrentLoadingBudgetProviderShould
    {

        public void SpendBudget()
        {
            // Arrange
            var initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingPerformanceBudget(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }


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
