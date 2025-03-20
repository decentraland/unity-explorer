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
        private static readonly OrdenedDataListComparer COMPARER_INSTANCE = new ();

        private List<OrderedDataList> orderedData;
        private readonly Entity playerEntity;
        private readonly Transform playerTransform;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        private readonly int maximumAmountOfScenesThatCanLoad;
        private readonly int maximumAmountOfReductedLODsThatCanLoad;
        private readonly int maximumAmoutOfLODsThatCanLoad;

        private int loadedScenes;
        private int loadedLODs;
        private int qualityReductedLOD;
        private int promisesCreated;

        internal ResolveSceneStateByIncreasingRadiusSystem(World world, IRealmPartitionSettings realmPartitionSettings, Entity playerEntity) : base(world)
        {
            playerTransform = World.Get<CharacterTransform>(playerEntity).Transform;
            this.playerEntity = playerEntity;
            this.realmPartitionSettings = realmPartitionSettings;

            maximumAmountOfScenesThatCanLoad = realmPartitionSettings.MaximumAmountOfScenesThatCanLoad;
            maximumAmoutOfLODsThatCanLoad = realmPartitionSettings.MaximumAmoutOfLODsThatCanLoad;
            maximumAmountOfReductedLODsThatCanLoad = realmPartitionSettings.MaximumAmountOfReductedLODsThatCanLoad;

            ResetUtilsArrays();
        }

        public void FinalizeComponents(in Query query)
        {
            //On realm change, reset the ordered data array
            ResetUtilsArrays();
        }

        private void ResetUtilsArrays()
        {
            // Set initial capacity to maximum amount of things that can load
            orderedData = new List<OrderedDataList>(maximumAmountOfScenesThatCanLoad + maximumAmoutOfLODsThatCanLoad + maximumAmountOfReductedLODsThatCanLoad);
        }

        protected override void Update(float t)
        {
            // Start a new loading if the previous batch is finished
            var anyNonEmpty = false;
            CheckAnyLoadingInProgressQuery(World, ref anyNonEmpty);

            if (!anyNonEmpty)
            {
                AddNewSceneDefinitionToListQuery(World);
                UpdatePlayerInParcel();

                //Why is there a 128B allocations here?
                orderedData.Sort(COMPARER_INSTANCE);
                ProcessVolatileRealmQuery(World);
                ProcessesFixedRealmQuery(World);
            }

            ProcessScenesUnloadingInRealmQuery(World);
        }

        private void UpdatePlayerInParcel()
        {
            Vector2Int currentParcel = playerTransform.position.ToParcel();

            for (var i = 0; i < orderedData.Count; i++)
                orderedData[i].UpdatePlayerInParcel(currentParcel);
        }

        [Query]
        [None(typeof(SceneLoadingState), typeof(DeleteEntityIntention), typeof(RoadInfo))]
        private void AddNewSceneDefinitionToList(in Entity entity, in PartitionComponent partitionComponent, in SceneDefinitionComponent sceneDefinitionComponent)
        {
            var sceneLoadingState = new SceneLoadingState();
            orderedData.Add(new OrderedDataList(entity, sceneDefinitionComponent, partitionComponent, sceneLoadingState));
            World.Add(entity, sceneLoadingState);
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
            CreatePromisesFromOrderedData(realmComponent.Ipfs);
        }

        [Query]
        [None(typeof(RoadInfo))]
        private void StartUnloading(in Entity entity, in PartitionComponent partitionComponent, in SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneLoadingState)
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
                CreatePromisesFromOrderedData(realmComponent.Ipfs);
        }

        private void CreatePromisesFromOrderedData(IIpfsRealm ipfsRealm)
        {
            loadedScenes = 0;
            loadedLODs = 0;
            qualityReductedLOD = 0;
            promisesCreated = 0;

            TeleportUtils.PlayerTeleportingState teleportParcel = TeleportUtils.GetTeleportParcel(World, playerEntity);

            //Dont do anything until the teleport cooldown is over
            if (teleportParcel.JustTeleported)
                return;

            for (var i = 0; i < orderedData.Count && promisesCreated < realmPartitionSettings.ScenesRequestBatchSize; i++)
            {
                OrderedDataList data = orderedData[i];

                //Ignore unpartitioned and out of range
                //Optimization: remove out of range from list when adding DeleteEntityIntention
                if (data.RawSqrDistance < 0 || data.OutOfRange) continue;

                if (teleportParcel.IsTeleporting)
                {
                    if (data.ParcelsHashSet.Contains(teleportParcel.Parcel))
                    {
                        UpdateLoadingState(ipfsRealm, data.Entity, data.SceneDefinitionComponent, data.PartitionComponent, data.SceneLoadingState);
                        break;
                    }
                    continue;
                }

                UpdateLoadingState(ipfsRealm, data.Entity, data.SceneDefinitionComponent, data.PartitionComponent, data.SceneLoadingState);
            }
        }

        private void TryUnload(in Entity entity, ref SceneLoadingState sceneState)
        {
            if (sceneState.Loaded)
                Unload(entity, ref sceneState);
        }

        private void Unload(in Entity entity, ref SceneLoadingState sceneState)
        {
            sceneState.VisualSceneState = VisualSceneStateEnum.UNINITIALIZED;
            sceneState.Loaded = false;
            sceneState.FullQuality = false;
            World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }

        private void UpdateLoadingState(IIpfsRealm ipfsRealm, in Entity entity, in SceneDefinitionComponent sceneDefinitionComponent, in PartitionComponent partitionComponent,
            SceneLoadingState sceneState)
        {
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
                        TryUnload(entity, ref sceneState);
                        candidateByEnum = VisualSceneStateEnum.UNINITIALIZED;
                    }
                    // Reduce the quality of this LOD if we have not yet hit the quality-reduction limit
                    sceneState.FullQuality = false;
                }
                else
                {
                    // Nothing else can load. And we need to unload the loaded which are still inside the loading range
                    TryUnload(entity, ref sceneState);
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
                    World.Add(entity, SceneLODInfo.Create());
                    break;
                default:
                    World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(ipfsRealm, sceneDefinitionComponent), partitionComponent));
                    break;
            }
        }

        public class OrdenedDataListComparer : IComparer<OrderedDataList>
        {
            public int Compare(OrderedDataList x, OrderedDataList y)
            {
                //Parcels in range should always have higher prioirty
                if (x.OutOfRange) return -1;
                if (y.OutOfRange) return 1;

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

        public class OrderedDataList
        {
            //Need it to get the ref of SceneLoadingState
            public Entity Entity;

            public readonly SceneDefinitionComponent SceneDefinitionComponent;
            public readonly SceneLoadingState SceneLoadingState;
            public readonly PartitionComponent PartitionComponent;

            public float RawSqrDistance;
            public bool IsBehind;
            public bool IsPlayerInsideParcel;
            public int XCoordinate;
            public bool OutOfRange;

            public readonly HashSet<Vector2Int> ParcelsHashSet;

            public OrderedDataList(Entity entity, SceneDefinitionComponent sceneDefinitionComponent, PartitionComponent partitionComponent, SceneLoadingState sceneLoadingState)
            {
                Entity = entity;
                SceneDefinitionComponent = sceneDefinitionComponent;
                SceneLoadingState = sceneLoadingState;
                PartitionComponent = partitionComponent;
                XCoordinate = sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.x;
                IsPlayerInsideParcel = false;
                RawSqrDistance = float.MaxValue;
                IsBehind = false;
                OutOfRange = true;

                //Cannot make spatial calculations, since a parcel can be contained inside the other
                //TODO: Is this the most performant way to do it?
                ParcelsHashSet = new HashSet<Vector2Int>();

                foreach (Vector2Int vector2Int in SceneDefinitionComponent.Parcels) { ParcelsHashSet.Add(vector2Int); }
            }

            public void UpdatePlayerInParcel(Vector2Int currentParcel)
            {
                //Maybe use ISCurrentAPI?
                IsPlayerInsideParcel = ParcelsHashSet.Contains(currentParcel);
                RawSqrDistance = PartitionComponent.RawSqrDistance;
                IsBehind = PartitionComponent.IsBehind;
                OutOfRange = PartitionComponent.OutOfRange;
            }
        }


    }




}
