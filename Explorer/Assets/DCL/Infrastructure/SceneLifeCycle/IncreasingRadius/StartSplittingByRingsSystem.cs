using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SingleScene;
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
        private readonly SingleSceneMode singleSceneMode;

        internal StartSplittingByRingsSystem(
            World world,
            IRealmPartitionSettings realmPartitionSettings,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper,
            SingleSceneMode singleSceneMode) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;
            this.singleSceneMode = singleSceneMode;
        }

        protected override void OnDispose()
        {
            parcelMathJobifiedHelper.Complete();
            parcelMathJobifiedHelper.Dispose();

            DisposeProcessedScenePointersQuery(World);
        }

        protected override void Update(float t)
        {
            ProcessRealmQuery(World);
        }

        [Query]
        private void DisposeProcessedScenePointers(ref ProcessedScenePointers processedScenePointers)
        {
            processedScenePointers.Value.Dispose();
        }

        [Query]
        [All(typeof(RealmComponent))]
        private void ProcessRealm(ref ProcessedScenePointers processedScenePointers)
        {
            StartSplittingQuery(World, in processedScenePointers);
        }

        [Query]
        private void StartSplitting([Data] in ProcessedScenePointers processedScenePointers, ref CameraSamplingData cameraSamplingData)
        {
            if (!cameraSamplingData.IsDirty) return;

            if (singleSceneMode.IsActive)
            {
                if (singleSceneMode.HasAnchor)
                    parcelMathJobifiedHelper.StartParcelsRingSplit(singleSceneMode.AnchorParcel.ToInt2(), 0, processedScenePointers.Value);

                return;
            }

            parcelMathJobifiedHelper.StartParcelsRingSplit(
                cameraSamplingData.Parcel.ToInt2(),
                realmPartitionSettings.MaxLoadingDistanceInParcels,
                processedScenePointers.Value);
        }
    }
}
