using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
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

        public override void Dispose()
        {
            base.Dispose();

            parcelMathJobifiedHelper.Dispose();
        }

        protected override void Update(float t)
        {
            ProcessRealmQuery(World);
        }

        [Query]
        [All(typeof(RealmComponent))]
        private void ProcessRealm(ref ProcessesScenePointers processesScenePointers)
        {
            StartSplittingQuery(World, in processesScenePointers);
        }

        [Query]
        private void StartSplitting([Data] in ProcessesScenePointers processesScenePointers, ref CameraSamplingData cameraSamplingData)
        {
            if (cameraSamplingData.IsDirty)
                parcelMathJobifiedHelper.StartParcelsRingSplit(
                    cameraSamplingData.Parcel.ToInt2(),
                    realmPartitionSettings.MaxLoadingDistanceInParcels,
                    processesScenePointers.Value);
        }
    }
}
