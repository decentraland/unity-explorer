using ECS.Prioritization.DeferredLoading;
using NUnit.Framework;
using System.Reflection;

namespace ECS.StreamableLoading.DeferredLoading.Tests
{
    [TestFixture]
    public class ConcurrentLoadingBudgetProviderShould
    {
        [Test]
        public void BudgetSpent()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

        [Test]
        public void BudgetRefilled()
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
        public void BudgetNotOverflowed()
        {
            // Arrange
            int initialBudget = 1;
            var budgetProvider = new ConcurrentLoadingBudgetProvider(initialBudget);

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());

            // Act
            budgetProvider.ReleaseBudget();
            budgetProvider.ReleaseBudget();
            budgetProvider.ReleaseBudget();

            // Assert (The second budget spend sould still be false, since the max budget is 1)
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());
            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }
    }
}
