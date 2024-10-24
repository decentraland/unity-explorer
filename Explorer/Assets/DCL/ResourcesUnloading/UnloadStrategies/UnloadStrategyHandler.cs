using System;
using ECS.Prioritization;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadStrategyHandler
    {
        internal UnloadStrategyBase[] unloadStrategies;
        private readonly ICacheCleaner cacheCleaner;

        private readonly int DEFAULT_FRAME_FAILURE_THRESHOLD = 250;

        public UnloadStrategyHandler(IRealmPartitionSettings realmPartitionSettings,
            ICacheCleaner cacheCleaner)
        {
            this.cacheCleaner = cacheCleaner;

            //The base strategy at 0 will always run
            //On top of that, we adds logic that run only if the previous one fails in an additive manner
            unloadStrategies = new UnloadStrategyBase[]
            {
                new StandardUnloadStrategy(DEFAULT_FRAME_FAILURE_THRESHOLD, cacheCleaner),
                new ReduceLoadingRadiusUnloadStrategy(DEFAULT_FRAME_FAILURE_THRESHOLD, realmPartitionSettings),
                new UnloadUnusedAssetUnloadStrategy(DEFAULT_FRAME_FAILURE_THRESHOLD)
            };
        }

        public void TryUnload()
        {
            for (var i = unloadStrategies.Length - 1; i >= 0; i--)
            {
                if (i == 0 || unloadStrategies[i - 1].FaillingOverThreshold())
                    unloadStrategies[i].TryUnload();
            }
        }

        public void ResetToNormal()
        {
            for (var i = 0; i < unloadStrategies.Length; i++)
                unloadStrategies[i].ResetStrategy();
        }

    }
}