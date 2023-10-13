using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     In case realm does not provide a fixed list of scenes scenes are loaded by radius,
    ///     Radius is gradually increased until the limit is reached (TODO)
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(CalculateParcelsInRangeSystem))]
    public partial class LoadPointersByRadiusSystem : LoadScenePointerSystemBase
    {
        internal LoadPointersByRadiusSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            // Resolve the current promise
            ResolveActivePromiseQuery(World);

            StartLoadingInRadiusQuery(World);
        }

        [Query]
        private void StartLoadingInRadius(ref RealmComponent realm, ref VolatileScenePointers volatileScenePointers, ref ParcelsInRange parcelsInRange, ref ProcessesScenePointers processesScenePointers)
        {
            // Create promises if they are not yet created
            // Promise is a bulk request
            // maintain one bulk request at a time
            if (volatileScenePointers.ActivePromise != null) return;

            // Check that there are no created scenes definitions for desired parcels already
            List<int2> input = volatileScenePointers.InputReusableList;

            foreach (int2 parcel in parcelsInRange.Value)
            {
                if (!processesScenePointers.Value.Contains(parcel))
                    input.Add(parcel);
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
