using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.Ipfs;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    public partial class ResolveSceneStateByIncreasingRadiusSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly OrdenedDataNativeComparer COMPARER_INSTANCE = new ();

        private readonly Entity playerEntity;
        private readonly Transform playerTransform;
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly IRealmData realmData;

        //Array sorting helpers
        private readonly List<OrderedDataManaged> orderedDataManaged;
        private NativeList<OrderedDataNative> orderedDataNative;
        internal JobHandle? sortingJobHandle;
        private bool arraysInSync;
        private bool inTeleport;


        //Loading helpers
        private int loadedScenes;
        private int loadedLODs;
        private int qualityReductedLOD;
        private int promisesCreated;

        private readonly SceneLoadingLimit sceneLoadingLimit;

        private readonly VisualSceneStateResolver visualSceneStateResolver;

        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings, Entity playerEntity,
            VisualSceneStateResolver visualSceneStateResolver, IRealmData realmData,
            SceneLoadingLimit sceneLoadingLimit) : base(world)
        {
            playerTransform = World.Get<CharacterTransform>(playerEntity).Transform;
            this.playerEntity = playerEntity;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.realmData = realmData;
            this.realmPartitionSettings = realmPartitionSettings;
            this.sceneLoadingLimit = sceneLoadingLimit;

            // Set initial capacity to 1/3 of the total capacity required for all rings
            int initialCapacity = ParcelMathJobifiedHelper.GetRingsArraySize(realmPartitionSettings.MaxLoadingDistanceInParcels) / 3;

            orderedDataManaged = new List<OrderedDataManaged>(initialCapacity);
            orderedDataNative = new NativeList<OrderedDataNative>(initialCapacity, Allocator.Persistent);

            ResetUtilsArrays();
        }

        public void FinalizeComponents(in Query query)
        {
            //On realm change, reset the ordered data array
            ResetUtilsArrays();
        }

        private void ResetUtilsArrays()
        {
            if (sortingJobHandle.HasValue)
                sortingJobHandle.Value.Complete();

            orderedDataManaged.Clear();
            orderedDataNative.Clear();
            arraysInSync = false;
        }

        protected override void Update(float t)
        {
            // Start a new loading if the previous batch is finished
            var anyNonEmpty = false;
            CheckAnyLoadingInProgressQuery(World, ref anyNonEmpty);

            if (!anyNonEmpty)
            {
                AddNewSceneDefinitionToListQuery(World);
                ProcessVolatileRealmQuery(World);
                ProcessesFixedRealmQuery(World);
            }

            ProcessScenesUnloadingInRealmQuery(World);
        }


        [Query]
        [None(typeof(SceneLoadingState), typeof(DeleteEntityIntention), typeof(RoadInfo))]
        private void AddNewSceneDefinitionToList(in Entity entity, in PartitionComponent partitionComponent,
            in SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.IsPortableExperience)
            {
                //Portable experiences shouldnt be analyzed. Create straight away
                World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                    new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent), partitionComponent), SceneLoadingState.CreatePortableExperience());
            }
            else
            {
                var sceneLoadingState = new SceneLoadingState();
                //Sizes should always be the same
                orderedDataManaged.Add(new OrderedDataManaged(entity, sceneDefinitionComponent, partitionComponent, sceneLoadingState));
                orderedDataNative.Add(new OrderedDataNative());
                arraysInSync = false;
                World.Add(entity, sceneLoadingState);
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
        private void ProcessVolatileRealm(ref RealmComponent realmComponent)
        {
            StartScenesLoading(realmComponent);
        }

        [Query]
        [None(typeof(RoadInfo))]
        private void StartUnloading(in Entity entity, in PartitionComponent partitionComponent, ref SceneLoadingState sceneLoadingState)
        {
            if (partitionComponent.OutOfRange)
                TryUnload(entity, ref sceneLoadingState);
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
        private void ProcessesFixedRealm(in RealmComponent realmComponent, ref FixedScenePointers fixedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved)
                StartScenesLoading(realmComponent);
        }

        private void StartScenesLoading(in RealmComponent realmComponent)
        {
            if (sortingJobHandle is { IsCompleted: true })
            {
                sortingJobHandle.Value.Complete();

                // Since adding new values is throttled, arrays may be out of sync. They need to be synced to work
                if (arraysInSync)
                    CreatePromisesFromOrderedData(realmComponent.Ipfs);
            }

            if (sortingJobHandle is { IsCompleted: false }) return;

            TeleportUtils.PlayerTeleportingState teleportParcel = TeleportUtils.GetTeleportParcel(World, playerEntity);
            int xCoordinate;
            int yCoordinate;

            if (teleportParcel.IsTeleporting)
            {
                xCoordinate = teleportParcel.Parcel.x;
                yCoordinate = teleportParcel.Parcel.y;
            }
            else
            {
                Vector2Int currentParcel = playerTransform.position.ToParcel();
                xCoordinate = currentParcel.x;
                yCoordinate = currentParcel.y;
            }

            unsafe
            {
                OrderedDataNative* dataPtr = orderedDataNative.GetUnsafePtr();

                for (var i = 0; i < orderedDataManaged.Count; i++)
                {
                    OrderedDataManaged currentOrderedData = orderedDataManaged[i];
                    dataPtr[i] = new OrderedDataNative
                    {
                        ReferenceListIndex = i,
                        RawSqrDistance = currentOrderedData.PartitionComponent.RawSqrDistance,
                        IsBehind = currentOrderedData.PartitionComponent.IsBehind,
                        IsPlayerInsideParcel = currentOrderedData.SceneDefinitionComponent.Contains(xCoordinate, yCoordinate),
                        XCoordinate = currentOrderedData.XCoordinate,
                        OutOfRange = currentOrderedData.PartitionComponent.OutOfRange,
                    };
                }
            }

            arraysInSync = true;
            inTeleport = teleportParcel.IsTeleporting;
            sortingJobHandle = orderedDataNative.SortJob(COMPARER_INSTANCE).Schedule();
        }

        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm)
        {
            loadedScenes = 0;
            loadedLODs = 0;
            qualityReductedLOD = 0;
            promisesCreated = 0;

            unsafe
            {
                int orderedDataNativeLength = orderedDataNative.Length;
                if (orderedDataNativeLength == 0) return;

                OrderedDataNative* dataPtr = orderedDataNative.GetUnsafePtr();

                if (inTeleport)
                {
                    //The parcel we are teleporting to should be the first one
                    OrderedDataManaged data = orderedDataManaged[dataPtr[0].ReferenceListIndex];
                    UpdateLoadingState(ipfsRealm, data.Entity, data.SceneDefinitionComponent, data.PartitionComponent, data.SceneLoadingState);
                    return;
                }

                for (var i = 0; i < orderedDataNativeLength && promisesCreated < realmPartitionSettings.ScenesRequestBatchSize; i++)
                {
                    OrderedDataManaged data = orderedDataManaged[dataPtr[i].ReferenceListIndex];

                    //Ignore unpartitioned and out of range
                    //Optimization: remove out of range from list when adding DeleteEntityIntention
                    if (dataPtr[i].RawSqrDistance < 0 || dataPtr[i].OutOfRange) continue;

                    UpdateLoadingState(ipfsRealm, data.Entity, data.SceneDefinitionComponent, data.PartitionComponent, data.SceneLoadingState);
                }
            }


        }

        private void TryUnload(in Entity entity, ref SceneLoadingState sceneState)
        {
            if (sceneState.PromiseCreated)
                Unload(entity, ref sceneState);
        }

        private void Unload(in Entity entity, ref SceneLoadingState sceneState)
        {
            sceneState.VisualSceneState = VisualSceneState.UNINITIALIZED;
            sceneState.PromiseCreated = false;
            sceneState.FullQuality = false;

            //We mark it as Defer because, down the line, the entity wont be deleted.
            //Either the LOD or the SceneFacade will be removed, but the Entity with the
            //SceneDefinitionComponent should persist
            World.Add(entity, new DeleteEntityIntention { DeferDeletion = true });
        }

        private void UpdateLoadingState(IIpfsRealm ipfsRealm, in Entity entity, in SceneDefinitionComponent sceneDefinitionComponent, in PartitionComponent partitionComponent,
            SceneLoadingState sceneState)
        {
            VisualSceneState candidateBy
                = visualSceneStateResolver.ResolveVisualSceneState(partitionComponent, sceneDefinitionComponent, sceneState.VisualSceneState, ipfsRealm.SceneUrns.Count > 0);

            //If we are over the amount of scenes that can be loaded, we downgrade quality to LOD
            if (candidateBy == VisualSceneState.SHOWING_SCENE && loadedScenes < sceneLoadingLimit.MaximumAmountOfScenesThatCanLoad)
                loadedScenes++;
            else
            {
                //Lets do a quality reduction analysis
                candidateBy = VisualSceneState.SHOWING_LOD;
            }

            //Reduce quality
            if (candidateBy == VisualSceneState.SHOWING_LOD)
            {
                if (loadedLODs < sceneLoadingLimit.MaximumAmountOfLODsThatCanLoad)
                {
                    // This LOD is within the full-quality limit, so load it normally. Nothing to do here
                    loadedLODs++;
                    sceneState.FullQuality = true;
                }
                else if (qualityReductedLOD < sceneLoadingLimit.MaximumAmountOfReductedLoDsThatCanLoad)
                {
                    qualityReductedLOD++;
                    if (sceneState.FullQuality)
                    {
                        //This wasnt previously quality reducted. Lets try to unload it and on next iteration we will try to load
                        TryUnload(entity, ref sceneState);
                        candidateBy = VisualSceneState.UNINITIALIZED;
                    }
                    // Reduce the quality of this LOD if we have not yet hit the quality-reduction limit
                    sceneState.FullQuality = false;
                }
                else
                {
                    // Nothing else can load. And we need to unload the loaded which are still inside the loading range
                    TryUnload(entity, ref sceneState);
                    candidateBy = VisualSceneState.UNINITIALIZED;
                }
            }

            //No new promise is required
            if (candidateBy == VisualSceneState.UNINITIALIZED
                || sceneState.VisualSceneState == candidateBy)
                return;

            promisesCreated++;
            sceneState.PromiseCreated = true;
            sceneState.VisualSceneState = candidateBy;

            switch (sceneState.VisualSceneState)
            {
                case VisualSceneState.SHOWING_LOD:
                    //The SceneLODInfo may still be in the entity, since it remains there until SceneIsReady (Check UnloadSceneLODInfoSystem)
                    //Therefore, we need to make this check because we dont want to break the entity mutual exclusive state
                    if (!World.Has<SceneLODInfo>(entity))
                        World.Add(entity, SceneLODInfo.Create());
                    break;
                default:
                    //The check is not needed here because the SceneFacade and promise are removed on the same frame that a SceneLODInfo was added
                    World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(ipfsRealm, sceneDefinitionComponent), partitionComponent));
                    break;
            }
        }



        public struct OrdenedDataNativeComparer : IComparer<OrderedDataNative>
        {
            public int Compare(OrderedDataNative x, OrderedDataNative y)
            {
                //Out of range always go last
                int compareOutOfRange = x.OutOfRange.CompareTo(y.OutOfRange);

                if (compareOutOfRange != 0)
                    return compareOutOfRange;

                //Parcels infront should always have higher priority
                int compareIsBehind = x.IsBehind.CompareTo(y.IsBehind);
                if (compareIsBehind != 0)
                    return compareIsBehind;

                if (x.IsPlayerInsideParcel && !y.IsPlayerInsideParcel) return -1;
                if (y.IsPlayerInsideParcel && !x.IsPlayerInsideParcel) return 1;

                // discrete distance comparison
                int bucketComparison = x.RawSqrDistance.CompareTo(y.RawSqrDistance);
                if (bucketComparison != 0)
                    return bucketComparison;

                //If everything fails, the scene on the right has higher priority
                return x.XCoordinate.CompareTo(y.XCoordinate);
            }
        }

        public struct OrderedDataNative
        {
            public int ReferenceListIndex;
            public float RawSqrDistance;
            public bool IsBehind;
            public bool IsPlayerInsideParcel;
            public int XCoordinate;
            public bool OutOfRange;
        }

        public class OrderedDataManaged
        {
            //Need it to get the ref of SceneLoadingState
            public Entity Entity;

            public readonly SceneDefinitionComponent SceneDefinitionComponent;
            public readonly SceneLoadingState SceneLoadingState;
            public readonly PartitionComponent PartitionComponent;

            public int XCoordinate;


            public OrderedDataManaged(Entity entity, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent, SceneLoadingState sceneLoadingState)
            {
                Entity = entity;
                SceneDefinitionComponent = sceneDefinitionComponent;
                SceneLoadingState = sceneLoadingState;
                PartitionComponent = partitionComponent;
                XCoordinate = sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.x;
            }

        }


    }
}
