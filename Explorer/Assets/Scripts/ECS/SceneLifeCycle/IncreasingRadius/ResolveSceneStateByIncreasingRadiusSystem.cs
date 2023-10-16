using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using Ipfs;
using Realm;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Pool;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    /// <summary>
    ///     Mutually exclusive to <see cref="ResolveSceneStateByRadiusSystem" />
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateAfter(typeof(CalculateParcelsInRangeSystem))]
    [UpdateAfter(typeof(CreateEmptyPointersInFixedRealmSystem))]
    public partial class ResolveSceneStateByIncreasingRadiusSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription START_SCENES_LOADING = new QueryDescription()
                                                                       .WithAll<SceneDefinitionComponent, PartitionComponent>()
                                                                       .WithNone<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>();

        private readonly IRealmPartitionSettings realmPartitionSettings;

        private struct OrderedData
        {
            public Entity Entity;
            public PartitionComponent PartitionComponent;
            public SceneDefinitionComponent DefinitionComponent;
        }

        private readonly List<OrderedData> orderedData;

        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;

            orderedData = ListPool<OrderedData>.Get();
        }

        public override void Dispose()
        {
            ListPool<OrderedData>.Release(orderedData);
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

            StartUnloadingQuery(World);
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
            // Order the scenes definitions by the CURRENT partition and serve first N of them

            orderedData.Clear();

            foreach (ref Chunk chunk in World.Query(in START_SCENES_LOADING))
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref SceneDefinitionComponent sceneDefinitionFirst = ref chunk.GetFirst<SceneDefinitionComponent>();
                ref PartitionComponent partitionComponentFirst = ref chunk.GetFirst<PartitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref SceneDefinitionComponent definition = ref Unsafe.Add(ref sceneDefinitionFirst, entityIndex);
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref PartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirst, entityIndex);

                    if (partitionComponent.RawSqrDistance >= maxLoadingSqrDistance) continue;

                    orderedData.Add(new OrderedData
                    {
                        Entity = entity,
                        PartitionComponent = partitionComponent,
                        DefinitionComponent = definition,
                    });
                }
            }

            // Raw Distance will give more stable results in terms of scenes loading order, especially in cases
            // when a wide range falls into the same bucket
            orderedData.Sort(static (p1, p2) => DistanceBasedComparer.INSTANCE.Compare(p1.PartitionComponent, p2.PartitionComponent));

            IIpfsRealm ipfsRealm = realmComponent.Ipfs;

            // Take first N
            for (var i = 0; i < orderedData.Count && i < realmPartitionSettings.ScenesRequestBatchSize; i++)
            {
                OrderedData data = orderedData[i];

                World.Add(data.Entity,
                    AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(ipfsRealm, data.DefinitionComponent.IpfsPath, data.DefinitionComponent.Definition, data.DefinitionComponent.Parcels, data.DefinitionComponent.IsEmpty),
                        data.PartitionComponent));
            }
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void StartUnloading(in Entity entity, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.Bucket < realmPartitionSettings.UnloadBucket) return;
            World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }
    }
}
