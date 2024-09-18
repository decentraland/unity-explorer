using System;
using ECS.Prioritization;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadStrategyHandler
    {
        private readonly IUnloadStrategy[] unloadStrategies;
        internal int currentUnloadStrategy;
        private readonly ICacheCleaner cacheCleaner;

        //Determines how many frames we need to fail to increase the aggressiveness tier
        private int consecutiveFailedFrames;
        private readonly int failuresFrameThreshold;


        public UnloadStrategyHandler(IRealmPartitionSettings realmPartitionSettings, int failuresFrameThreshold,
            ICacheCleaner cacheCleaner)
        {
            this.cacheCleaner = cacheCleaner;
            this.failuresFrameThreshold = failuresFrameThreshold;
            currentUnloadStrategy = 0;

            //Higher the index, more aggressive the strategy
            unloadStrategies = new IUnloadStrategy[]
            {
                new StandardUnloadStrategy(),
                new AggressiveUnloadStrategy(realmPartitionSettings)
            };
        }

        public void TryUnload()
        {
            if (IsRunning())
                return;

            consecutiveFailedFrames++;
            if (consecutiveFailedFrames > failuresFrameThreshold)
                IncreaseAggresivenessTier();

            unloadStrategies[currentUnloadStrategy].TryUnload(cacheCleaner);
        }

        private bool IsRunning()
        {
            return unloadStrategies[currentUnloadStrategy].IsRunning;
        }

        public void ResetToNormal()
        {
            currentUnloadStrategy = 0;
            consecutiveFailedFrames = 0;
        }

        private void IncreaseAggresivenessTier()
        {
            currentUnloadStrategy = Math.Clamp(currentUnloadStrategy + 1, 0, unloadStrategies.Length - 1);
            consecutiveFailedFrames = 0;
        }
    }
}