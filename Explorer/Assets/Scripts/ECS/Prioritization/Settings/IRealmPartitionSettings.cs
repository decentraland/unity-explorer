using ECS.Prioritization.Components;

namespace ECS.Prioritization
{
    /// <summary>
    ///     Stores partition settings for scenes
    /// </summary>
    public interface IRealmPartitionSettings
    {
        /// <summary>
        ///     Maximum tolerance between the last processed camera rotation and the current one to skip resorting of the worlds.
        ///     This value must be much bigger than <see cref="IPartitionSettings.AngleTolerance" />
        /// </summary>
        float AggregateAngleTolerance { get; }

        /// <summary>
        ///     Maximum tolerance between the last processed camera position and the current one to skip resorting of the worlds.
        ///     This value must be much bigger than <see cref="IPartitionSettings.PositionSqrTolerance" />
        /// </summary>
        float AggregatePositionSqrTolerance { get; }

        /// <summary>
        ///     The hard distance limit after which scenes and scenes definitions do not load
        /// </summary>
        int MaxLoadingDistanceInParcels { get; }

        /// <summary>
        ///     Bucket from which the scenes start to unload, the distance corresponding to this bucket should be bigger than <see cref="MaxLoadingDistanceInParcels" />
        ///     so distant parcels will start unloading gradually
        /// </summary>
        int UnloadBucket { get; }

        /// <summary>
        ///     The number of closest scenes that can be requested at a time
        /// </summary>
        int ScenesRequestBatchSize { get; }

        /// <summary>
        ///     The number of scenes definitions that can be requested a time within the same list request
        /// </summary>
        int ScenesDefinitionsRequestBatchSize { get; }

        /// <summary>
        ///     Get the desired update frequency for a scene in a given partition.
        ///     It should take both bucket index and isBehind into account.
        /// </summary>
        /// <returns>FPS</returns>
        int GetSceneUpdateFrequency(in PartitionComponent partition);
    }
}
