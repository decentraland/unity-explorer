using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : UnloadStrategyBase
    {
        private readonly ICacheCleaner cacheCleaner;

        public override void RunStrategy()
        {
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }

        public StandardUnloadStrategy(int failureThreshold, ICacheCleaner cacheCleaner) : base(failureThreshold)
        {
            this.cacheCleaner = cacheCleaner;
        }
    }
}