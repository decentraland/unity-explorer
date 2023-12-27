using DCL.Optimization.PerformanceBudgeting;

namespace DCL.Profiles
{
    public interface IProfileCache
    {
        int Count { get; }

        Profile? Get(string id);

        void Set(string id, Profile profile);

        void Unload(IConcurrentBudgetProvider concurrentBudgetProvider, int maxAmount);
    }
}
