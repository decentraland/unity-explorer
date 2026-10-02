using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SingleScene;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CheckCameraQualifiedForRepartitioningSystem))]
    public partial class StartSplittingByRingsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly SingleSceneMode singleSceneMode;

        private Vector2Int? lastSplitAnchor;

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

        public void FinalizeComponents(in Query query)
        {
            lastSplitAnchor = null;
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
            if (singleSceneMode.IsActive)
            {
                SplitAroundAnchor(in processedScenePointers);
                return;
            }

            lastSplitAnchor = null;

            if (cameraSamplingData.IsDirty)
                parcelMathJobifiedHelper.StartParcelsRingSplit(
                    cameraSamplingData.Parcel.ToInt2(),
                    realmPartitionSettings.MaxLoadingDistanceInParcels,
                    processedScenePointers.Value);
        }

        private void SplitAroundAnchor(in ProcessedScenePointers processedScenePointers)
        {
            if (!singleSceneMode.HasAnchor)
            {
                lastSplitAnchor = null;
                return;
            }

            Vector2Int anchor = singleSceneMode.AnchorParcel;

            if (lastSplitAnchor == anchor)
                return;

            lastSplitAnchor = anchor;

            parcelMathJobifiedHelper.StartParcelsRingSplit(anchor.ToInt2(), 0, processedScenePointers.Value);
        }
    }
}
