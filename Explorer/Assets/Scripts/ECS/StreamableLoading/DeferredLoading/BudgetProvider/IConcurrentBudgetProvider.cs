namespace ECS.Prioritization.DeferredLoading
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget();

        void ReleaseBudget();
    }
}
