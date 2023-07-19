namespace ECS.BudgetProvider
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget(int budgetCost) =>
            true;

        public void ReleaseBudget(int budgetToRelease) { }
    }
}
