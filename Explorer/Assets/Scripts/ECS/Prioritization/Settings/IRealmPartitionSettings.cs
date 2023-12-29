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
        ///     Tolerance that is added to <see cref="MaxLoadingDistanceInParcels" /> to determine the distance at which scenes start unloading.
        ///     It should be slightly bigger than 0 to avoid scenes unloading and loading back immediately when the player moves back and forth
        ///     when the <see cref="MaxLoadingDistanceInParcels" /> is reached
        /// </summary>
        int UnloadingDistanceToleranceInParcels { get; }

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
