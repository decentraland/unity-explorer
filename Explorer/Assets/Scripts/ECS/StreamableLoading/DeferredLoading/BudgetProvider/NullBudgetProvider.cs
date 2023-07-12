namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
