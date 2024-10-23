namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public abstract class UnloadStrategy
    {
        internal int currentFailureCount;
        internal int failureThreshold;

        protected UnloadStrategy(int failureThreshold)
        {
            this.failureThreshold = failureThreshold;
        }

        public virtual void ResetStrategy()
        {
            currentFailureCount = 0;
        }

        public bool FaillingOverThreshold()
        {
            return currentFailureCount >= failureThreshold;
        }

        public abstract void RunStrategy();

        public void TryUnload()
        {
            RunStrategy();
            currentFailureCount++;
        }
    }
}