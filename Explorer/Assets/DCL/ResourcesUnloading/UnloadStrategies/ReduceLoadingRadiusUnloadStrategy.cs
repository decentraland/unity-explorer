using ECS.Prioritization;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class ReduceLoadingRadiusUnloadStrategy : UnloadStrategy
    {
        private readonly IRealmPartitionSettings realmPartitionSettings;

        public ReduceLoadingRadiusUnloadStrategy(UnloadStrategy previousStrategy, IRealmPartitionSettings realmPartitionSettings) : base(previousStrategy)
        {
            this.realmPartitionSettings = realmPartitionSettings;
        }

        protected override void RunStrategy(ICacheCleaner cacheCleaner)
        {
            //Forces MaxLoadingDistanceInParcels to the minimum value
            //TODO (Juani): A message warning that the distance has been silently modified
            realmPartitionSettings.MaxLoadingDistanceInParcels = realmPartitionSettings.MinLoadingDistanceInParcels;

        }
    }
}
