using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.Prioritization.Components;

namespace ECS.Prioritization.Systems
{
    /// <summary>
    ///     <para>
    ///         Runs in a global world, checks if camera position has changed enough to be qualified for re-partitioning.
    ///         In comparison to <see cref="CheckCameraQualifiedForRepartitioningSystem" /> allows much bigger tolerance to prevent
    ///         resorting worlds too often.
    ///     </para>
    ///     <para>
    ///         If qualified for repartitioning it updates the order in which ECS worlds are executed
    ///     </para>
    ///     <para>
    ///         Executes in `LateUpdate` as all movement is happening in `Update`
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class SortWorldsAggregateSystem : BaseUnityLoopSystem
    {
        private readonly ISystemGroupAggregate<IPartitionComponent>.IFactory partitionedWorldsAggregateFactory;
        private readonly IRealmPartitionSettings partitionSettings;

        internal SortWorldsAggregateSystem(World world,
            ISystemGroupAggregate<IPartitionComponent>.IFactory partitionedWorldsAggregateFactory,
            IRealmPartitionSettings partitionSettings) : base(world)
        {
            this.partitionedWorldsAggregateFactory = partitionedWorldsAggregateFactory;
            this.partitionSettings = partitionSettings;
        }

        protected override void Update(float t)
        {
            CheckCameraTransformChangedQuery(World);
        }

        [Query]
        private void CheckCameraTransformChanged(ref RealmSamplingData samplingData, ref CameraComponent cameraComponent)
        {
            // if camera transform changed significantly re-sort all loaded worlds
            if (ScenesPartitioningUtils.TryUpdateCameraTransformOnChanged(samplingData, in cameraComponent,
                    partitionSettings.AggregatePositionSqrTolerance, partitionSettings.AggregateAngleTolerance))
            {
                PlayerLoopHelper.GetAggregates<IPartitionedWorldsAggregate, IPartitionComponent>(partitionedWorldsAggregateFactory, samplingData.Aggregates);

                for (var i = 0; i < samplingData.Aggregates.Count; i++)
                    samplingData.Aggregates[i].UpdateSorting();
            }
        }
    }
}
