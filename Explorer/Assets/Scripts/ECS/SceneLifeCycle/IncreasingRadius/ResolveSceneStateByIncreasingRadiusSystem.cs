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
                                                                       .WithNone<DeleteEntityIntention, EmptySceneComponent, RoadInfo>();

        internal JobHandle? sortingJobHandle;
        private NativeList<OrderedData> orderedData;
        private readonly Entity playerEntity;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        //TODO: Do we need it?
        private readonly UnloadingSceneCounter unloadingSceneCounter;

        private readonly int maximumAmountOfScenesThatCanLoad;
        private readonly int maximumAmountOfReductedLODsThatCanLoad;
        private readonly int maximumAmoutOfLODsThatCanLoad;

        private int loadedScenes;
        private int loadedLODs;
        private int qualityReductedLOD;
        private int promisesCreated;


        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
            this.realmPartitionSettings = realmPartitionSettings;

            maximumAmountOfScenesThatCanLoad = realmPartitionSettings.MaximumAmountOfScenesThatCanLoad;
            maximumAmoutOfLODsThatCanLoad = realmPartitionSettings.MaximumAmoutOfLODsThatCanLoad;
            maximumAmountOfReductedLODsThatCanLoad = realmPartitionSettings.MaximumAmountOfReductedLODsThatCanLoad;

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
                ProcessVolatileRealmQuery(World);
                ProcessesFixedRealmQuery(World);
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
        private void ProcessVolatileRealm(ref RealmComponent realm)
        {
            StartScenesLoading(ref realm);
        }

        [Query]
        private void StartUnloading(in Entity entity, in PartitionComponent partitionComponent, in SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneLoadingState)
        {
            if (partitionComponent.OutOfRange)
                TryUnload(entity, sceneDefinitionComponent, ref sceneLoadingState);
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
        private void ProcessesFixedRealm(ref RealmComponent realmComponent, ref FixedScenePointers fixedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved)
                StartScenesLoading(ref realmComponent);
        }

        private void StartScenesLoading(ref RealmComponent realmComponent)
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

                    //Ignore unpartitioned and out of range
                    if (partitionComponent.RawSqrDistance < 0 || partitionComponent.OutOfRange) continue;

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

        public bool AreListsEqual(NativeList<OrderedData> list1, NativeList<OrderedData> list2)
        {
            if (list1.Length != list2.Length)
                return false;

            for (var i = 0; i < list1.Length; i++)
            {
                if (!list1[i].Equals(list2[i]))
                    return false;
            }

            return true;
        }

        private NativeList<OrderedData> previousOrderedData = new (Allocator.Persistent);

        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm)
        {
            unloadingSceneCounter.UpdateUnloadingScenes();
            loadedScenes = 0;
            loadedLODs = 0;
            qualityReductedLOD = 0;
            promisesCreated = 0;

            PlayerTeleportingState teleportParcel = GetTeleportParcel();

            if (previousOrderedData.Length > 0)
            {
                if (!AreListsEqual(previousOrderedData, orderedData))
                    UnityEngine.Debug.Log("JUANI THEY ARE DFFIERENT");
            }

            for (var i = 0; i < orderedData.Length && promisesCreated < realmPartitionSettings.ScenesRequestBatchSize; i++)
            {
                OrderedData data = orderedData[i];

                if (!World.IsAlive(data.Entity))
                    return;

                // We can't save component to data as sorting is throttled and components could change
                // TODO: We need to optimize this
                var components
                    = World.Get<SceneDefinitionComponent, PartitionComponent, SceneLoadingState>(data.Entity);

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

            previousOrderedData = orderedData;
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

        private void TryUnload(in Entity entity, SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneState)
        {
            if (sceneState.Loaded)
            {
                if (World.TryGet(entity, out ISceneFacade sceneFacade))
                    unloadingSceneCounter.RegisterSceneFacade(sceneDefinitionComponent.Definition.id, sceneFacade);

                Unload(entity, ref sceneState);
            }
        }

        private void Unload(in Entity entity, ref SceneLoadingState sceneState)
        {
            sceneState.VisualSceneState = VisualSceneStateEnum.UNINITIALIZED;
            sceneState.Loaded = false;
            sceneState.FullQuality = false;
            World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }

        //TODO: Requires re-analysis every frame? Partition changes when bucket changes, but now we have the memory restriction
        private void UpdateLoadingState(IIpfsRealm ipfsRealm, OrderedData data, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent,
            ref SceneLoadingState sceneState)
        {
            //Dont try to load an unloading scene. Wait
            if (unloadingSceneCounter.IsSceneUnloading(sceneDefinitionComponent.Definition.id))
                return;

            VisualSceneStateEnum candidateByEnum
                = VisualSceneStateResolver.ResolveVisualSceneState(partitionComponent, sceneDefinitionComponent, sceneState.VisualSceneState);

            //If we are over the amount of scenes that can be loaded, we downgrade quality to LOD
            if (candidateByEnum == VisualSceneStateEnum.SHOWING_SCENE && loadedScenes < maximumAmountOfScenesThatCanLoad)
                loadedScenes++;
            else
            {
                //Lets do a quality reduction analysis
                candidateByEnum = VisualSceneStateEnum.SHOWING_LOD;
            }

            //Reduce quality
            if (candidateByEnum == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (loadedLODs < maximumAmoutOfLODsThatCanLoad)
                {
                    // This LOD is within the full-quality limit, so load it normally. Nothing to do here
                    loadedLODs++;
                    sceneState.FullQuality = true;
                }
                else if (qualityReductedLOD < maximumAmountOfReductedLODsThatCanLoad)
                {
                    qualityReductedLOD++;
                    if (sceneState.FullQuality)
                    {
                        //This wasnt previously quality reducted. Lets try to unload it and on next iteration we will try to load
                        TryUnload(data.Entity, sceneDefinitionComponent, ref sceneState);
                        candidateByEnum = VisualSceneStateEnum.UNINITIALIZED;
                    }
                    // Reduce the quality of this LOD if we have not yet hit the quality-reduction limit
                    sceneState.FullQuality = false;
                }
                else
                {
                    // Nothing else can load. And we need to unload the loaded which are still inside the loading range
                    TryUnload(data.Entity, sceneDefinitionComponent, ref sceneState);
                    candidateByEnum = VisualSceneStateEnum.UNINITIALIZED;
                }
            }

            //No new promise is required
            if (candidateByEnum == VisualSceneStateEnum.UNINITIALIZED
                || sceneState.VisualSceneState == candidateByEnum)
                return;

            promisesCreated++;
            sceneState.Loaded = true;
            sceneState.VisualSceneState = candidateByEnum;

            switch (sceneState.VisualSceneState)
            {
                case VisualSceneStateEnum.SHOWING_LOD:
                    World.Add(data.Entity, SceneLODInfo.Create());
                    break;
                default:
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

        public struct OrderedData
        {
            /// <summary>
            ///     Referencing entity is expensive and at the moment we don't delete scene entities at all
            /// </summary>
            public Entity Entity;
            public DistanceBasedComparer.DataSurrogate Data;
        }
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
