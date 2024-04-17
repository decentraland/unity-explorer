using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SceneFacade;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DCL.LOD.Components;
using System;
using DCL.LOD;
using DCL.Roads.Components;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateAfter(typeof(CreateEmptyPointersInFixedRealmSystem))]
    public partial class ResolveSceneStateByIncreasingRadiusSystem : BaseUnityLoopSystem
    {
        private static readonly Comparer COMPARER_INSTANCE = new ();

        private static readonly QueryDescription START_SCENES_LOADING = new QueryDescription()
            .WithAll<SceneDefinitionComponent, PartitionComponent, VisualSceneState>()
            .WithNone<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>, SceneLODInfo, RoadInfo>();

        private readonly IRealmPartitionSettings realmPartitionSettings;

        internal JobHandle? sortingJobHandle;

        private NativeList<OrderedData> orderedData;

        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;

            // Set initial capacity to 1/3 of the total capacity required for all rings
            orderedData = new NativeList<OrderedData>(
                ParcelMathJobifiedHelper.GetRingsArraySize(realmPartitionSettings.MaxLoadingDistanceInParcels) / 3,
                Allocator.Persistent);
        }

        public override void Dispose()
        {
            orderedData.Dispose();
        }

        protected override void Update(float t)
        {
            // Start a new loading if the previous batch is finished
            var anyNonEmpty = false;
            CheckAnyLoadingInProgressQuery(World, ref anyNonEmpty);

            if (!anyNonEmpty)
            {
                float maxLoadingDistance = realmPartitionSettings.MaxLoadingDistanceInParcels * ParcelMathHelper.PARCEL_SIZE;
                float maxLoadingSqrDistance = maxLoadingDistance * maxLoadingDistance;

                ProcessVolatileRealmQuery(World, maxLoadingSqrDistance);
                ProcessesFixedRealmQuery(World, maxLoadingSqrDistance);
            }

            float unloadingDistance = (Mathf.Max(1, realmPartitionSettings.UnloadingDistanceToleranceInParcels) + realmPartitionSettings.MaxLoadingDistanceInParcels)
                                      * ParcelMathHelper.PARCEL_SIZE;

            float unloadingSqrDistance = unloadingDistance * unloadingDistance;

            ProcessScenesUnloadingInRealmQuery(World, unloadingSqrDistance);
        }

        [Query]
        [All(typeof(PartitionComponent), typeof(SceneDefinitionComponent))]
        [None(typeof(ISceneFacade))]
        private void CheckAnyLoadingInProgress([Data] ref bool anyNonEmpty, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            anyNonEmpty |= !promise.IsConsumed;
        }

        [Query]
        [None(typeof(StaticScenePointers), typeof(FixedScenePointers))]
        private void ProcessVolatileRealm([Data] float maxLoadingSqrDistance, ref RealmComponent realm)
        {
            StartScenesLoading(ref realm, maxLoadingSqrDistance);
        }

        [Query]
        [None(typeof(StaticScenePointers))]
        [All(typeof(RealmComponent))]
        private void ProcessScenesUnloadingInRealm([Data] float unloadingSqrDistance)
        {
            StartUnloadingQuery(World, unloadingSqrDistance);
        }

        /// <summary>
        ///     Start loading scenes when all fixed pointers are loaded, otherwise we can't
        ///     weigh them against each other, and may start loading distant scenes first
        /// </summary>
        [Query]
        [None(typeof(StaticScenePointers), typeof(VolatileScenePointers))]
        private void ProcessesFixedRealm([Data] float maxLoadingSqrDistance, ref RealmComponent realmComponent, ref FixedScenePointers fixedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved)
                StartScenesLoading(ref realmComponent, maxLoadingSqrDistance);
        }

        private void StartScenesLoading(ref RealmComponent realmComponent, float maxLoadingSqrDistance)
        {
            if (sortingJobHandle is { IsCompleted: true })
            {
                sortingJobHandle.Value.Complete();
                CreatePromisesFromOrderedData(realmComponent.Ipfs);
            }

            if (sortingJobHandle is { IsCompleted: false }) return;

            // Start new sorting
            // Order the scenes definitions by the CURRENT partition and serve first N of them

            orderedData.Clear();

            foreach (ref Chunk chunk in World.Query(in START_SCENES_LOADING))
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref PartitionComponent partitionComponentFirst = ref chunk.GetFirst<PartitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref PartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirst, entityIndex);

                    if (partitionComponent.RawSqrDistance >= maxLoadingSqrDistance) continue;

                    orderedData.Add(new OrderedData
                    {
                        Entity = entity,
                        Data = new DistanceBasedComparer.DataSurrogate(partitionComponent.RawSqrDistance, partitionComponent.IsBehind),
                    });
                }
            }

            // Raw Distance will give more stable results in terms of scenes loading order, especially in cases
            // when a wide range falls into the same bucket
            sortingJobHandle = orderedData.SortJob(COMPARER_INSTANCE).Schedule();
        }

        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm)
        {
            var promisesCreated = 0;

            for (var i = 0; i < orderedData.Length && promisesCreated < realmPartitionSettings.ScenesRequestBatchSize; i++)
            {
                OrderedData data = orderedData[i];

                // As sorting is throttled Entity might gone out of scope
                if (!World.IsAlive(data.Entity))
                    continue;

                // We can't save component to data as sorting is throttled and components could change
                var components
                    = World.Get<SceneDefinitionComponent, PartitionComponent, VisualSceneState>(data.Entity);

                switch (components.t2.Value.CurrentVisualSceneState)
                {
                    case VisualSceneStateEnum.SHOWING_LOD:
                        World.Add(data.Entity, SceneLODInfo.Create());
                        break;
                    case VisualSceneStateEnum.ROAD:
                        World.Add(data.Entity, RoadInfo.Create());
                        break;
                    default:
                        CreateSceneFacadePromise.Execute(World, data.Entity, ipfsRealm, components.t0, components.t1.Value);
                        break;
                }

                promisesCreated++;
            }
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        [Any(typeof(SceneLODInfo), typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(RoadInfo))]
        private void StartUnloading([Data] float unloadingSqrDistance, in Entity entity, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.RawSqrDistance < unloadingSqrDistance) return;
            World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }

        /// <summary>
        ///     It must be a structure to be compatible with Burst SortJob
        /// </summary>
        private struct Comparer : IComparer<OrderedData>
        {
            public int Compare(OrderedData x, OrderedData y) =>
                DistanceBasedComparer.Compare(x.Data, y.Data);
        }

        private struct OrderedData
        {
            /// <summary>
            ///     Referencing entity is expensive and at the moment we don't delete scene entities at all
            /// </summary>
            public Entity Entity;
            public DistanceBasedComparer.DataSurrogate Data;
        }
    }
}
