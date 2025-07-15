using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Utility;

namespace SceneLifeCycle.IncreasingRadius
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
        private void ProcessRealm(Entity entity, ref ProcessedScenePointers processedScenePointers)
        {
            IReadOnlyList<int2>? pendingScenePointers = World.TryGet(entity, out VolatileScenePointers volatileScenePointers) && volatileScenePointers.ActivePromise.HasValue
                ? volatileScenePointers.ActivePromise.Value.LoadingIntention.Pointers
                : Array.Empty<int2>();

            StartSplittingQuery(World, in processedScenePointers, pendingScenePointers);
        }

        [Query]
        private void StartSplitting([Data] in ProcessedScenePointers processedScenePointers, [Data] IReadOnlyList<int2> pendingScenePointers, ref CameraSamplingData cameraSamplingData)
        {
            if (cameraSamplingData.IsDirty)
                parcelMathJobifiedHelper.StartParcelsRingSplit(
                    cameraSamplingData.Parcel.ToInt2(),
                    realmPartitionSettings.MaxLoadingDistanceInParcels,
                    processedScenePointers.Value,
                    pendingScenePointers);
        }
    }
}
