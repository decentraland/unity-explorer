using System;
using ECS.Prioritization;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class ReduceLoadingRadiusUnloadStrategy : UnloadStrategyBase
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;

        public ReduceLoadingRadiusUnloadStrategy(int failureThreshold, IRealmPartitionSettings realmPartitionSettings) :
            base(failureThreshold)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public override void RunStrategy()
        {
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;
        }

    }
}
