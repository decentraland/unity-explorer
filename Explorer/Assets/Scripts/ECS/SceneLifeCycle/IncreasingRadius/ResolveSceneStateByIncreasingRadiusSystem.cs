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
            public IpfsTypes.IpfsPath IpfsPath;
            public IpfsTypes.SceneEntityDefinition SceneEntityDefinition;
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
            float maxLoadingDistance = realmPartitionSettings.MaxLoadingDistanceInParcels * ParcelMathHelper.PARCEL_SIZE;
            float maxLoadingSqrDistance = maxLoadingDistance * maxLoadingDistance;

            ProcessVolatileRealmQuery(World, maxLoadingSqrDistance);
            ProcessesFixedRealmQuery(World, maxLoadingSqrDistance);
            StartUnloadingQuery(World);
        }

        [Query]
        [None(typeof(StaticScenePointers), typeof(FixedScenePointers))]
        private void ProcessVolatileRealm([Data] float maxLoadingSqrDistance, ref RealmComponent realm)
        {
            StartScenesLoading(realm.Ipfs, maxLoadingSqrDistance);
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
                StartScenesLoading(realmComponent.Ipfs, maxLoadingSqrDistance);
        }

        private void StartScenesLoading(IIpfsRealm ipfsRealm, float maxLoadingSqrDistance)
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

                    if (definition.IsEmpty) continue;
                    if (partitionComponent.RawSqrDistance >= maxLoadingSqrDistance) continue;

                    orderedData.Add(new OrderedData
                    {
                        Entity = entity,
                        IpfsPath = definition.IpfsPath,
                        SceneEntityDefinition = definition.Definition,
                        PartitionComponent = partitionComponent,
                    });
                }
            }

            orderedData.Sort(static (p1, p2) => p1.PartitionComponent.CompareTo(p2.PartitionComponent));

            // Take first N
            for (var i = 0; i < orderedData.Count && i < realmPartitionSettings.ScenesRequestBatchSize; i++)
            {
                OrderedData data = orderedData[i];

                World.Add(data.Entity,
                    AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World, new GetSceneFacadeIntention(ipfsRealm, data.IpfsPath, data.SceneEntityDefinition), data.PartitionComponent));
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void StartUnloading(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent)
        {
            if (sceneDefinitionComponent.IsEmpty) return;
            if (partitionComponent.Bucket < realmPartitionSettings.UnloadBucket) return;
            World.Add(entity, new DeleteEntityIntention());
        }
    }
}
