namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget();

        void ReleaseBudget();
    }
}
