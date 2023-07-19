namespace ECS.BudgetProvider
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget(int budgetCost = 1);

        void ReleaseBudget(int budgetToRelease = 1);
    }
}
