using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Abstract;
using ECS.Prioritization.Components;

namespace ECS.Prioritization.Systems
{
    /// <summary>
    ///     <para>
    ///         Runs in a global world, checks if camera position has changed enough to be qualified for re-partitioning
    ///     </para>
    ///     <para>
    ///         Executes in `LateUpdate` as all movement is happening in `Update`
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CheckCameraQualifiedForRepartitioningSystem : BaseUnityLoopSystem
    {
        private readonly IPartitionSettings partitionSettings;

        internal CheckCameraQualifiedForRepartitioningSystem(World world, IPartitionSettings partitionSettings) : base(world)
        {
            this.partitionSettings = partitionSettings;
        }

        protected override void Update(float t)
        {
            CheckCameraTransformChangedQuery(World);
        }

        [Query]
        private void CheckCameraTransformChanged(ref CameraSamplingData cameraSamplingData, ref CameraComponent cameraComponent)
        {
            ScenesPartitioningUtils.CheckCameraTransformChanged(cameraSamplingData, in cameraComponent, partitionSettings.PositionSqrTolerance, partitionSettings.AngleTolerance);
        }
    }
}
