using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : UnloadStrategy
    {
        
        public StandardUnloadStrategy() : base(null)
        {
        }

        protected override void RunStrategy(ICacheCleaner cacheCleaner)
        {
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }
    }
}