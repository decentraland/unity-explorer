using System;
using ECS.Prioritization;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class ReduceLoadingRadiusUnloadStrategy : UnloadStrategy
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;

        public ReduceLoadingRadiusUnloadStrategy(IRealmPartitionSettings realmPartitionSettings)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public override void TryUnload(ICacheCleaner cacheCleaner)
        {
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;
            base.TryUnload(cacheCleaner);
        }

    }
}
