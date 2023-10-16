using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    /// <summary>
    ///     Mutually exclusive to <see cref="LoadPointersByRadiusSystem" />
    /// </summary>
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadPointersByIncreasingRadiusSystem : LoadScenePointerSystemBase
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        private bool splitIsPending;

        internal LoadPointersByIncreasingRadiusSystem(World world,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper,
            IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;
            this.realmPartitionSettings = realmPartitionSettings;
        }

        protected override void Update(float t)
        {
            // VolatileScenePointers should be created from RealmController

            // job started means that there was a new split initiated (dirty state)
            if (parcelMathJobifiedHelper.JobStarted)
            {
                // no matter what finish the current split
                parcelMathJobifiedHelper.Complete();
                splitIsPending = true;
            }

            ResolveActivePromiseQuery(World);
            StartLoadingFromVolatilePointersQuery(World);
        }

        /// <summary>
        ///     We can't use this flow if realm/world provides with a fixed pointers list
        ///     as EntitiesActiveEndpoint is not supported by its content server
        /// </summary>
        [Query]
        [None(typeof(FixedScenePointers))]
        private void StartLoadingFromVolatilePointers(ref RealmComponent realm, ref VolatileScenePointers volatileScenePointers)
        {
            if (!splitIsPending) return;

            // maintain one bulk request at a time
            if (volatileScenePointers.ActivePromise != null) return;

            // Take up to <ScenesDefinitionsRequestBatchSize> closest pointers that were not processed yet
            List<int2> input = volatileScenePointers.InputReusableList;

            ref readonly NativeArray<ParcelMathJobifiedHelper.ParcelInfo> flatArray = ref parcelMathJobifiedHelper.LastSplit;
            int i;

            for (i = 0; i < flatArray.Length; i++)
            {
                ParcelMathJobifiedHelper.ParcelInfo parcelInfo = flatArray[i];

                if (parcelInfo.AlreadyProcessed)
                    continue;

                if (input.Count < realmPartitionSettings.ScenesDefinitionsRequestBatchSize)
                    input.Add(parcelInfo.Parcel);
            }

            // Detect if we have other unprocessed parcels in the current split
            splitIsPending = false;

            for (; i < flatArray.Length; i++)
            {
                if (!flatArray[i].AlreadyProcessed)
                {
                    splitIsPending = true;
                    break;
                }
            }

            if (input.Count == 0) return;

            volatileScenePointers.ActivePromise
                = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(volatileScenePointers.RetrievedReusableList, input, new CommonLoadingArguments(realm.Ipfs.EntitiesActiveEndpoint)), PartitionComponent.TOP_PRIORITY);
        }

        [Query]
        private void ResolveActivePromise(ref VolatileScenePointers volatileScenePointers, ref ProcessesScenePointers processesScenePointers)
        {
            if (!volatileScenePointers.ActivePromise.HasValue) return;

            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = volatileScenePointers.ActivePromise.Value;

            if (!promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result)) return;

            // contains the list of parcels that were requested
            IReadOnlyList<int2> requestedList = promise.LoadingIntention.Pointers;

            if (result.Succeeded)
            {
                List<IpfsTypes.SceneEntityDefinition> definitions = result.Asset.Value;

                for (var i = 0; i < definitions.Count; i++)
                {
                    IpfsTypes.SceneEntityDefinition scene = definitions[i];
                    if (scene.pointers.Count == 0) continue;

                    TryCreateSceneEntity(scene, new IpfsTypes.IpfsPath(scene.id, URLDomain.EMPTY), processesScenePointers.Value);
                }

                // Empty parcels = parcels for which no scene pointers were retrieved
                for (var i = 0; i < requestedList.Count; i++)
                {
                    int2 parcel = requestedList[i];
                    if (!processesScenePointers.Value.Add(parcel)) continue;
                    World.Create(new SceneDefinitionComponent(parcel.ToVector2Int()));
                }
            }
            else
            {
                // Signal that those parcels should not be requested again
                for (var i = 0; i < requestedList.Count; i++)
                    processesScenePointers.Value.Add(requestedList[i]);
            }

            volatileScenePointers.ActivePromise = null;
            volatileScenePointers.InputReusableList.Clear();
            volatileScenePointers.RetrievedReusableList.Clear();
        }
    }
}
