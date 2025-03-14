using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class ResolveSceneStateByIncreasingRadiusSystem : BaseUnityLoopSystem
    {
        private static readonly Comparer COMPARER_INSTANCE = new ();

        private static readonly QueryDescription START_SCENES_LOADING = new QueryDescription()
           .WithAll<SceneDefinitionComponent, PartitionComponent>();

        private readonly IRealmPartitionSettings realmPartitionSettings;
        internal JobHandle? sortingJobHandle;
        private NativeList<OrderedData> orderedData;
        private readonly Entity playerEntity;

        private readonly UnloadingSceneCounter unloadingSceneCounter;


        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings, Entity playerEntity) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.playerEntity = playerEntity;

            // Set initial capacity to 1/3 of the total capacity required for all rings
            orderedData = new NativeList<OrderedData>(
                ParcelMathJobifiedHelper.GetRingsArraySize(realmPartitionSettings.MaxLoadingDistanceInParcels) / 3,
                Allocator.Persistent);

            unloadingSceneCounter = new UnloadingSceneCounter();
        }

        protected override void OnDispose()
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

            ProcessScenesUnloadingInRealmQuery(World);
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
        private void ProcessScenesUnloadingInRealm()
        {
            StartUnloadingQuery(World);
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

            Vector2Int playerParcel = World.Get<CharacterTransform>(playerEntity).Transform.position.ToParcel();

            foreach (ref Chunk chunk in World.Query(in START_SCENES_LOADING))
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref PartitionComponent partitionComponentFirst = ref chunk.GetFirst<PartitionComponent>();
                ref SceneDefinitionComponent sceneDefinitionComponentFirst = ref chunk.GetFirst<SceneDefinitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref PartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirst, entityIndex);
                    ref SceneDefinitionComponent sceneDefinitionComponent = ref Unsafe.Add(ref sceneDefinitionComponentFirst, entityIndex);

                    if (partitionComponent.RawSqrDistance >= maxLoadingSqrDistance || partitionComponent.RawSqrDistance < 0) continue;

                    orderedData.Add(new OrderedData
                    {
                        Entity = entity,
                        Data = new DistanceBasedComparer.DataSurrogate(partitionComponent.RawSqrDistance, partitionComponent.IsBehind, sceneDefinitionComponent.ContainsParcel(playerParcel), sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.x),
                    });
                }
            }

            // Raw Distance will give more stable results in terms of scenes loading order, especially in cases
            // when a wide range falls into the same bucket
            sortingJobHandle = orderedData.SortJob(COMPARER_INSTANCE).Schedule();
        }

        private readonly int scenesToLoad = 4;


        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm)
        {
            unloadingSceneCounter.UpdateUnloadingScenes();

            for (var i = 0; i < orderedData.Length; i++)
            {
                OrderedData data = orderedData[i];

                // As sorting is throttled Entity might gone out of scope
                if (!World.IsAlive(data.Entity))
                    continue;

                // We can't save component to data as sorting is throttled and components could change
                var components
                    = World.Get<SceneDefinitionComponent, PartitionComponent>(data.Entity);

                //If there is a teleport intent, only allow to load the teleport intent
                if (TeleportOccuring(ipfsRealm, data, components.t0.Value, components.t1.Value))
                    continue;

                if (i < scenesToLoad)
                    TryLoad(ipfsRealm, data, components.t0.Value, components.t1.Value);
                else
                    TryUnload(data, components.t0.Value);
            }
        }

        private bool TeleportOccuring(IIpfsRealm ipfsRealm, OrderedData data, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent)
        {
            if (World.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                if (sceneDefinitionComponent.ContainsParcel(playerTeleportIntent.Parcel))
                    TryLoad(ipfsRealm, data, sceneDefinitionComponent, partitionComponent);

                return true;
            }

            return false;
        }

        private void TryUnload(OrderedData data, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (World.Has<VisualSceneState>(data.Entity))
            {
                if (World.TryGet(data.Entity, out ISceneFacade sceneFacade))
                    unloadingSceneCounter.RegisterSceneFacade(sceneDefinitionComponent.Definition.id, sceneFacade);
                World.Add(data.Entity, DeleteEntityIntention.DeferredDeletion);
            }
        }

        private void TryLoad(IIpfsRealm ipfsRealm, OrderedData data, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent)
        {
            if (!World.Has<VisualSceneState>(data.Entity))
            {
                //Dont try to load an unloading scene. Wait
                if (unloadingSceneCounter.IsSceneUnloading(sceneDefinitionComponent.Definition.id))
                    return;

                var visualSceneState = new VisualSceneState();
                VisualSceneStateResolver.ResolveVisualSceneState(ref visualSceneState, partitionComponent, sceneDefinitionComponent);

                switch (visualSceneState.CurrentVisualSceneState)
                {
                    case VisualSceneStateEnum.SHOWING_LOD:
                        World.Add(data.Entity, visualSceneState, SceneLODInfo.Create());
                        break;
                    case VisualSceneStateEnum.ROAD:
                        World.Add(data.Entity, visualSceneState, RoadInfo.Create());
                        break;
                    default:
                        World.Add(data.Entity, visualSceneState, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                            new GetSceneFacadeIntention(ipfsRealm, sceneDefinitionComponent), partitionComponent));

                        break;
                }
            }
        }

        [Query]
        [All(typeof(SceneDefinitionComponent))]
        [None(typeof(DeleteEntityIntention))]
        [Any(typeof(SceneLODInfo), typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(RoadInfo))]
        private void StartUnloading(in Entity entity, ref PartitionComponent partitionComponent)
        {
            if (partitionComponent.OutOfRange)
            {
                World.Add(entity, DeleteEntityIntention.DeferredDeletion);
            }
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

    public class UnloadingSceneCounter
    {
        private readonly Dictionary<string, ISceneFacade> unloadingScenesDictionary = new (100);
        private readonly HashSet<string> unloadingScenes = new ();
        private readonly List<string> reusableScenesToRemove = new ();

        public void RegisterSceneFacade(string sceneId, ISceneFacade facade)
        {
            unloadingScenesDictionary[sceneId] = facade;
        }

        public void UpdateUnloadingScenes()
        {
            reusableScenesToRemove.Clear();

            foreach (KeyValuePair<string, ISceneFacade> keyValuePair in unloadingScenesDictionary)
            {
                if (keyValuePair.Value.SceneStateProvider.State == SceneState.Disposed)
                    reusableScenesToRemove.Add(keyValuePair.Key);
            }

            foreach (string sceneId in reusableScenesToRemove)
            {
                unloadingScenesDictionary.Remove(sceneId);
                unloadingScenes.Remove(sceneId);
            }
        }

        public bool IsSceneUnloading(string sceneId) =>
            unloadingScenes.Contains(sceneId);
    }
}
