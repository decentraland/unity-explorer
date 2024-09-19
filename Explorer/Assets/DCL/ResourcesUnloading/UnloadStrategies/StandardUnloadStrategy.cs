using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : IUnloadStrategy
    {
        public bool IsRunning => false;


        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            Debug.Log("JUANI RUNNING THE STANDARD");
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }
    }
}