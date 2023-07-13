namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget(int budgetCost);

        void ReleaseBudget(int budgetReleased);
    }
}
