namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class NoAcquiredBudget : IAcquiredBudget
    {
        public static readonly NoAcquiredBudget INSTANCE = new ();

        private NoAcquiredBudget() { }

        public void Dispose() { }

        public void Release() { }
    }
}
