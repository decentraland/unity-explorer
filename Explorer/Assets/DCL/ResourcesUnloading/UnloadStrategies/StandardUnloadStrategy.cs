using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : IUnloadStrategy
    {
        public bool IsRunning => false;


        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }
    }
}