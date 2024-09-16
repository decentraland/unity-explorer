using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : IUnloadStrategy
    {
        public bool isRunning => false;


        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            Debug.Log("JUANI RUNNING STANDAARD");

            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }
    }
}