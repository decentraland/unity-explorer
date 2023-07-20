using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Prioritization.Systems;
using ECS.SceneLifeCycle.Components;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CheckCameraQualifiedForRepartitioningSystem))]
    public partial class StartSplittingByRingsSystem : BaseUnityLoopSystem
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        internal StartSplittingByRingsSystem(
            World world,
            IRealmPartitionSettings realmPartitionSettings,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;
        }

        protected override void Update(float t)
        {
            StartSplittingQuery(World);
        }

        [Query]
        private void StartSplitting(ref CameraSamplingData cameraSamplingData, ref VolatileScenePointers volatileScenePointers)
        {
            if (cameraSamplingData.IsDirty)
                parcelMathJobifiedHelper.StartParcelsRingSplit(
                    cameraSamplingData.Parcel.ToInt2(),
                    realmPartitionSettings.MaxLoadingDistanceInParcels,
                    volatileScenePointers.ProcessedParcels);
        }
    }
}
