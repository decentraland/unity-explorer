using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using Unity.Mathematics;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CheckCameraQualifiedForRepartitioningSystem))]
    public partial class StartSplittingByRingsSystem : BaseUnityLoopSystem
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        private readonly LandscapeParcelService parcelService;
        private FetchParcelResult fetchParcelResult;
        private bool updatedProcessedScenePointers;
        private bool gotParcelManifest;
        


        internal StartSplittingByRingsSystem(
            World world,
            IRealmPartitionSettings realmPartitionSettings,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper,
            IWebRequestController requestController) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;

            parcelService = new LandscapeParcelService(requestController, false);
            GetParcelManifest();
        }

        private async UniTask GetParcelManifest()
        {
            fetchParcelResult = await parcelService.LoadManifestAsync(new CancellationToken());
            gotParcelManifest = true;
        }
        

        public override void Dispose()
        {
            base.Dispose();

            parcelMathJobifiedHelper.Complete();
            parcelMathJobifiedHelper.Dispose();

            DisposeProcessedScenePointersQuery(World);
        }

        protected override void Update(float t)
        {
            if (!gotParcelManifest)
                return;


            UpdateProcessedScenePointersQuery(World);
            ProcessRealmQuery(World);
        }

        [Query]
        private void DisposeProcessedScenePointers(ref ProcessedScenePointers processedScenePointers)
        {
            processedScenePointers.Value.Dispose();
        }

        [Query]
        [All(typeof(RealmComponent))]
        private void UpdateProcessedScenePointers(ref ProcessedScenePointers processedScenePointers)
        {
            if (gotParcelManifest && !updatedProcessedScenePointers)
            {
                updatedProcessedScenePointers = true;
                foreach (var parcel in fetchParcelResult.Manifest.GetEmptyParcels())
                {
                    processedScenePointers.Value.Add(new int2(parcel.x, parcel.y));
                }

                foreach (var parcel in fetchParcelResult.Manifest.GetRoads())
                {
                    processedScenePointers.Value.Add(new int2(parcel.x, parcel.y));
                }

                updatedProcessedScenePointers = true;
            }
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
            if (gotParcelManifest && updatedProcessedScenePointers)
            {
                if (cameraSamplingData.IsDirty)
                    parcelMathJobifiedHelper.StartParcelsRingSplit(
                        cameraSamplingData.Parcel.ToInt2(),
                        realmPartitionSettings.MaxLoadingDistanceInParcels,
                        processedScenePointers.Value);
            }
        }
    }
}
