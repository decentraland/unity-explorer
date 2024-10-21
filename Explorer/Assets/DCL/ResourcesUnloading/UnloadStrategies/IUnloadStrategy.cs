namespace DCL.ResourcesUnloading.UnloadStrategies
{
    
    public interface IUnloadStrategy
    {
        void ResetStrategy();
        void TryUnload(ICacheCleaner cacheCleaner);
        bool FailedOverThreshold();
    }
    
    public abstract class UnloadStrategy : IUnloadStrategy
    {

        private int currentFailureCount;
        private readonly int FAILURE_THRESHOLD = 250;

        public virtual void ResetStrategy()
        {
            currentFailureCount = 0;
        }

        public bool FailedOverThreshold()
        {
            return currentFailureCount > FAILURE_THRESHOLD;
        }

        public virtual void TryUnload(ICacheCleaner cacheCleaner)
        {
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
            currentFailureCount++;
        }
    }
}