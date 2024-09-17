using DCL.Optimization.PerformanceBudgeting;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public interface IUnloadStrategy
    {
        void TryUnload(ICacheCleaner cacheCleaner);

        bool IsRunning { get; }
    }
}