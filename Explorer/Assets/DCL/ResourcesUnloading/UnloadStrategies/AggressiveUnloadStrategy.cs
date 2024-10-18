using System;
using Cysharp.Threading.Tasks;
using ECS.Prioritization;
using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;

        public AggressiveUnloadStrategy(IRealmPartitionSettings realmPartitionSettings)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        public void TryUnload(ICacheCleaner cacheCleaner)
        {
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;
            cacheCleaner.UnloadCache();
            cacheCleaner.UpdateProfilingCounters();
        }

    }
}
