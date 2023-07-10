using ECS.Prioritization.DeferredLoading;
using NUnit.Framework;
using System;

namespace ECS.StreamableLoading.DeferredLoading.Tests
{
    [TestFixture]
    public class ConcurrentLoadingBudgetProviderShould
    {
        [Test]
        public void SpendBudget()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

        [Test]
        public void RefillBudget()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

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
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());

            // Act
            budgetProvider.ReleaseBudget();
            Assert.Throws<Exception>(() => budgetProvider.ReleaseBudget());
        }
    }
}
