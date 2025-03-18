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
                                                                       .WithAll<SceneDefinitionComponent, PartitionComponent, SceneLoadingState>()
                                                                       .WithNone<DeleteEntityIntention, EmptySceneComponent>();

        private readonly IRealmPartitionSettings realmPartitionSettings;
        internal JobHandle? sortingJobHandle;
        private NativeList<OrderedData> orderedData;
        private readonly Entity playerEntity;

        private readonly UnloadingSceneCounter unloadingSceneCounter;
        private readonly int maximumAmountOfScenesThatCanLoad = 5;
        private readonly int maximumAmountOfScenesLODsThatCanLoad = 5;

        private int loadedScenes;
        private int loadedLODs;


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
            StartScenesLoading(maxLoadingSqrDistance, ref realm);
        }

        //TODO: StaticScenePointers should never unload
        /*[Query]
        [None(typeof(StaticScenePointers))]
        [All(typeof(RealmComponent))]
        private void ProcessScenesUnloadingInRealm()
        {
            StartUnloadingQuery(World);
        }
        */

        /// <summary>
        ///     Start loading scenes when all fixed pointers are loaded, otherwise we can't
        ///     weigh them against each other, and may start loading distant scenes first
        /// </summary>
        [Query]
        [None(typeof(StaticScenePointers), typeof(VolatileScenePointers))]
        private void ProcessesFixedRealm([Data] float maxLoadingSqrDistance, ref RealmComponent realmComponent, ref FixedScenePointers fixedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved)
                StartScenesLoading(maxLoadingSqrDistance, ref realmComponent);
        }

        private void StartScenesLoading([Data] float maxLoadingSqrDistance, ref RealmComponent realmComponent)
        {
            if (sortingJobHandle is { IsCompleted: true })
            {
                sortingJobHandle.Value.Complete();
                CreatePromisesFromOrderedData(realmComponent.Ipfs, maxLoadingSqrDistance);
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

                    //Ignore unpartitioned
                    if (partitionComponent.RawSqrDistance < 0) continue;

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


        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm, float maxLoadingSqrDistance)
        {
            unloadingSceneCounter.UpdateUnloadingScenes();
            loadedScenes = 0;
            loadedLODs = 0;

            PlayerTeleportingState teleportParcel = GetTeleportParcel();

            for (var i = 0; i < orderedData.Length; i++)
            {
                OrderedData data = orderedData[i];

                if (!World.IsAlive(data.Entity))
                    return;

                // We can't save component to data as sorting is throttled and components could change
                // We need to optimize this
                var components
                    = World.Get<SceneDefinitionComponent, PartitionComponent, SceneLoadingState>(data.Entity);

                //Always unload out of range scenes
                if (components.t1.Value.RawSqrDistance >= maxLoadingSqrDistance)
                {
                    TryUnload(data, components.t0.Value, ref components.t2.Value);
                    continue;
                }

                //If there is a teleport intent, only allow to load the teleport intent
                //TODO: This is going to work better when the intent is according to the player position and not the camera
                //Right now, the reliance on "isPLayerInsideParcel" doesnt work, since the player is not moved to the parcel until the teleport is complete
                //Therefore, a scene can unload/load until the player is moved
                if (teleportParcel.IsTeleporting)
                {
                    if (components.t0.Value.ContainsParcel(teleportParcel.Parcel))
                    {
                        UpdateLoadingState(ipfsRealm, data, components.t0.Value, components.t1.Value, ref components.t2.Value);
                        break;
                    }
                    continue;
                }

                UpdateLoadingState(ipfsRealm, data, components.t0.Value, components.t1.Value, ref components.t2.Value);
            }
        }

        private PlayerTeleportingState GetTeleportParcel()
        {
            var teleportParcel = new PlayerTeleportingState();

            if (World.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                teleportParcel.IsTeleporting = true;
                teleportParcel.Parcel = playerTeleportIntent.Parcel;
            }

            return teleportParcel;
        }

        private bool TeleportOccuring(IIpfsRealm ipfsRealm, OrderedData data, SceneDefinitionComponent sceneDefinitionComponent,
            PartitionComponent partitionComponent, ref SceneLoadingState sceneState)
        {
            if (World.TryGet(playerEntity, out PlayerTeleportIntent playerTeleportIntent))
            {
                if (sceneDefinitionComponent.ContainsParcel(playerTeleportIntent.Parcel))
                    UpdateLoadingState(ipfsRealm, data, sceneDefinitionComponent, partitionComponent, ref sceneState);
                return true;
            }

            return false;
        }

        private void TryUnload(OrderedData data, SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneState)
        {
            if (sceneState.loaded)
            {
                if (World.TryGet(data.Entity, out ISceneFacade sceneFacade))
                    unloadingSceneCounter.RegisterSceneFacade(sceneDefinitionComponent.Definition.id, sceneFacade);

                Unload(data, ref sceneState);
            }
        }

        private void Unload(OrderedData data, ref SceneLoadingState sceneState)
        {
            sceneState.VisualSceneStateEnum = VisualSceneStateEnum.UNINITIALIZED;
            sceneState.loaded = false;
            World.Add(data.Entity, DeleteEntityIntention.DeferredDeletion);
        }


        private void UpdateLoadingState(IIpfsRealm ipfsRealm, OrderedData data, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent,
            ref SceneLoadingState sceneState)
        {
            //Dont try to load an unloading scene. Wait
            if (unloadingSceneCounter.IsSceneUnloading(sceneDefinitionComponent.Definition.id))
                return;

            //TODO: Requires re-analysis every frame? Partition changes when bucket changes, but now we have the memory restriction
            //if (sceneState.VisualSceneStateEnum != VisualSceneStateEnum.UNINITIALIZED && !partitionComponent.IsDirty)
            //    return;

            //TODO: Optimize road code. For now, the only thing that can unload a road is distance
            //Maybe add the component on the `SceneEntityDefinition` creation?
            if (sceneState.VisualSceneStateEnum == VisualSceneStateEnum.ROAD)
                return;

            VisualSceneStateEnum candidateByEnum
                = VisualSceneStateResolver.ResolveVisualSceneState(partitionComponent, sceneDefinitionComponent, sceneState.VisualSceneStateEnum);

            //If we are over the amount of scenes that can be loaded, we downgrade quality to LOD
            if (candidateByEnum == VisualSceneStateEnum.SHOWING_SCENE && loadedScenes >= maximumAmountOfScenesThatCanLoad)
                candidateByEnum = VisualSceneStateEnum.SHOWING_LOD;

            //TODO: Reduce quality, dont unload
            if (candidateByEnum == VisualSceneStateEnum.SHOWING_LOD && loadedLODs >= maximumAmountOfScenesLODsThatCanLoad)
            {
                TryUnload(data, sceneDefinitionComponent, ref sceneState);
                return;
            }

            //Nothing has changed, keep going
            if (sceneState.VisualSceneStateEnum == candidateByEnum)
                return;

            sceneState.loaded = true;
            sceneState.VisualSceneStateEnum = candidateByEnum;

            switch (sceneState.VisualSceneStateEnum)
            {
                case VisualSceneStateEnum.SHOWING_LOD:
                    loadedLODs++;
                    World.Add(data.Entity, SceneLODInfo.Create());
                    break;
                case VisualSceneStateEnum.ROAD:
                    World.Add(data.Entity, RoadInfo.Create());
                    break;
                default:
                    loadedScenes++;
                    World.Add(data.Entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(ipfsRealm, sceneDefinitionComponent), partitionComponent));
                    break;
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

    public struct SceneLoadingState
    {
        public bool loaded;
        public VisualSceneStateEnum VisualSceneStateEnum;
    }

    public struct PlayerTeleportingState
    {
        public Vector2Int Parcel;
        public bool IsTeleporting;
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
