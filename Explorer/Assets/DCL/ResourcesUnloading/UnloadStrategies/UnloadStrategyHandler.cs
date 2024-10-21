using System;
using ECS.Prioritization;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadStrategyHandler
    {
        internal IUnloadStrategy[] unloadStrategies;
        internal int currentUnloadStrategy;
        private readonly ICacheCleaner cacheCleaner;

        public UnloadStrategyHandler(IRealmPartitionSettings realmPartitionSettings,
            ICacheCleaner cacheCleaner)
        {
            this.cacheCleaner = cacheCleaner;
            currentUnloadStrategy = 0;

            //Higher the index, more aggressive the strategy
            unloadStrategies = new IUnloadStrategy[]
            {
                new StandardUnloadStrategy(),
                new ReduceLoadingRadiusUnloadStrategy(realmPartitionSettings),
                new UnloadUnusedAssetUnloadStrategy()
            };
        }

        public void TryUnload()
        {
             unloadStrategies[currentUnloadStrategy].TryUnload(cacheCleaner);
            if( unloadStrategies[currentUnloadStrategy].FailedOverThreshold())
                IncreaseAggresivenessTier();
        }


        public void ResetToNormal()
        {
            currentUnloadStrategy = 0;
            for (var i = 0; i < unloadStrategies.Length; i++)
                unloadStrategies[i].ResetStrategy();
        }

        private void IncreaseAggresivenessTier()
        {
            currentUnloadStrategy = Math.Clamp(currentUnloadStrategy + 1, 0, unloadStrategies.Length - 1);
        }
    }
}