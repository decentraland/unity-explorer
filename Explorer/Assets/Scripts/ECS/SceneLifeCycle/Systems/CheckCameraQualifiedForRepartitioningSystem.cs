using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;

namespace ECS.SceneLifeCycle.Systems
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
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class CheckCameraQualifiedForRepartitioningSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;
        private readonly IPartitionSettings partitionSettings;
        private readonly CameraSamplingData cameraSamplingData;

        internal CheckCameraQualifiedForRepartitioningSystem(World world, IPartitionSettings partitionSettings, IRealmData realmData, CameraSamplingData cameraSamplingData) : base(world)
        {
            this.partitionSettings = partitionSettings;
            this.realmData = realmData;
            this.cameraSamplingData = cameraSamplingData;
        }

        protected override void Update(float t)
        {
            // it should be updated only if realm is already loaded
            if (!realmData.Configured)
                return;

            cameraSamplingData.IsDirty = false;
            ModifyCameraSamplingBasedOnTeleportIntentQuery(World);
            CheckCameraTransformChangedQuery(World);
        }

        [Query]
        private void CheckCameraTransformChanged(ref CameraSamplingData cameraSamplingData, ref CameraComponent cameraComponent)
        {
            if (cameraSamplingData.IsDirty)
                return;

            ScenesPartitioningUtils.CheckCameraTransformChanged(cameraSamplingData, in cameraComponent, partitionSettings.PositionSqrTolerance, partitionSettings.AngleTolerance);
        }

        [Query]
        private void ModifyCameraSamplingBasedOnTeleportIntent(in PlayerTeleportIntent teleportIntent)
        {
            cameraSamplingData.Position = teleportIntent.Position;
            cameraSamplingData.Parcel = teleportIntent.Parcel;
            cameraSamplingData.IsDirty = true;
        }
    }
}
