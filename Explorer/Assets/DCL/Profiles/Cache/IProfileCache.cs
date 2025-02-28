using DCL.Optimization.PerformanceBudgeting;

namespace DCL.Profiles
{
    public interface IProfileCache
    {
        Profile? Get(string id);

        Profile? GetByUserName(string userName);

        void Set(string id, Profile profile);

        void Remove(string id);

        void Unload(IPerformanceBudget concurrentBudgetProvider, int maxAmount);
    }
}
