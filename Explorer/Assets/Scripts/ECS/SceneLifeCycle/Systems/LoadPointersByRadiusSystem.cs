using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System.Collections.Generic;
using UnityEngine;
using Utility.Pool;

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

            InitiateDefinitionsLoadingQuery(World);
            StartLoadingInRadiusQuery(World);
        }

        [Query]
        [All(typeof(ParcelsInRange))]
        [None(typeof(VolatileScenePointers), typeof(StaticScenePointers))]
        private void InitiateDefinitionsLoading(in Entity entity, ref RealmComponent realmComponent)
        {
            if (realmComponent.ScenesAreFixed) return;

            // Tolerate allocation - once per realm only
            World.Add(entity, new VolatileScenePointers(
                new List<IpfsTypes.SceneEntityDefinition>(PoolConstants.SCENES_COUNT),
                new HashSet<Vector2Int>(PoolConstants.SCENES_COUNT * 4),
                new List<Vector2Int>(PoolConstants.SCENES_COUNT)));
        }

        [Query]
        private void StartLoadingInRadius(ref RealmComponent realm, ref VolatileScenePointers volatileScenePointers, ref ParcelsInRange parcelsInRange)
        {
            // Create promises if they are not yet created
            // Promise is a bulk request
            // maintain one bulk request at a time
            if (volatileScenePointers.ActivePromise != null) return;

            // Check that there are no created scenes definitions for desired parcels already
            List<Vector2Int> input = volatileScenePointers.InputReusableList;

            foreach (Vector2Int parcel in parcelsInRange.Value)
            {
                if (!volatileScenePointers.ProcessedParcels.Contains(parcel))
                    input.Add(parcel);
            }

            if (input.Count == 0) return;

            volatileScenePointers.ActivePromise
                = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(volatileScenePointers.RetrievedReusableList, input, new CommonLoadingArguments(realm.Ipfs.EntitiesActiveEndpoint)), PartitionComponent.TOP_PRIORITY);
        }

        [Query]
        private void ResolveActivePromise(ref VolatileScenePointers volatileScenePointers)
        {
            if (!volatileScenePointers.ActivePromise.HasValue) return;

            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = volatileScenePointers.ActivePromise.Value;

            if (!promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result)) return;

            // contains the list of parcels that were requested
            IReadOnlyList<Vector2Int> requestedList = promise.LoadingIntention.Pointers;

            if (result.Succeeded)
            {
                List<IpfsTypes.SceneEntityDefinition> definitions = result.Asset.Value;

                for (var i = 0; i < definitions.Count; i++)
                {
                    IpfsTypes.SceneEntityDefinition scene = definitions[i];
                    if (scene.pointers.Count == 0) continue;

                    CreateSceneEntity(scene, new IpfsTypes.IpfsPath(scene.id, string.Empty), out Vector2Int[] sceneParcels);

                    for (var j = 0; j < sceneParcels.Length; j++)
                        volatileScenePointers.ProcessedParcels.Add(sceneParcels[j]);
                }

                // Empty parcels = parcels for which no scene pointers were retrieved
                for (var i = 0; i < requestedList.Count; i++)
                {
                    Vector2Int parcel = requestedList[i];
                    if (!volatileScenePointers.ProcessedParcels.Add(parcel)) continue;
                    World.Create(new SceneDefinitionComponent(parcel));
                }
            }
            else
            {
                // Signal that those parcels should not be requested again
                for (var i = 0; i < requestedList.Count; i++)
                {
                    Vector2Int failedParcel = requestedList[i];
                    volatileScenePointers.ProcessedParcels.Add(failedParcel);
                }
            }

            volatileScenePointers.ActivePromise = null;
            volatileScenePointers.InputReusableList.Clear();
            volatileScenePointers.RetrievedReusableList.Clear();
        }
    }
}
