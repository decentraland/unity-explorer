namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget(int budgetCost) =>
            true;

        public void ReleaseBudget(int budgetReleased) { }
    }
}
