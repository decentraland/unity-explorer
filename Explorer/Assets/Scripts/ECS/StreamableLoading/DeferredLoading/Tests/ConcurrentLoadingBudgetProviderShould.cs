using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
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
            Assert.AreEqual(true, budgetProvider.TrySpendBudget(1));
            Assert.AreEqual(false, budgetProvider.TrySpendBudget(1));
        }

        [Test]
        public void RefillBudget()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget(1));
            Assert.AreEqual(false, budgetProvider.TrySpendBudget(1));

            // Act
            budgetProvider.ReleaseBudget(1);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget(1));
        }

        [Test]
        public void BudgetOverflowThrown()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget(1));
            Assert.AreEqual(false, budgetProvider.TrySpendBudget(1));

            // Act
            budgetProvider.ReleaseBudget(1);
            Assert.Throws<Exception>(() => budgetProvider.ReleaseBudget(1));
        }
    }
}
