namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public abstract class UnloadStrategy 
    {
        private readonly UnloadStrategy? PreviousStrategy;

        internal int currentFailureCount;
        internal int FAILURE_THRESHOLD = 300;

        protected UnloadStrategy(UnloadStrategy? previousStrategy)
        {
            this.PreviousStrategy = previousStrategy;
        }
        
        protected abstract void RunStrategy(ICacheCleaner cacheCleaner);
        protected virtual void ResetStrategy() {}

        private bool FailedOverThreshold()
        {
            return currentFailureCount >= FAILURE_THRESHOLD;
        }
        
        public void Reset()
        {
            currentFailureCount = 0;
            ResetStrategy();
            PreviousStrategy?.Reset();
        }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            if (PreviousStrategy == null || PreviousStrategy.FailedOverThreshold())
            {
                RunStrategy(cacheCleaner);
                currentFailureCount++;
            }
            PreviousStrategy?.TryUnload(cacheCleaner);
        }
    }
}