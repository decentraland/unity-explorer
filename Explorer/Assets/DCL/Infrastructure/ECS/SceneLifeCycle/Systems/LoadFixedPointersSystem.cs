using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Loads scene definition from fixed scene pointers if the realm provides them
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadFixedPointersSystem : LoadScenePointerSystemBase
    {
        internal LoadFixedPointersSystem(World world, IRealmData realmData) : base(world, new HashSet<Vector2Int>(), realmData) { }

        protected override void Update(float t)
        {
            ResolvePromisesQuery(World);
            InitiateDefinitionLoadingQuery(World);
        }

        [Query]
        [None(typeof(FixedScenePointers))]
        private void InitiateDefinitionLoading(in Entity entity, ref RealmComponent realmComponent)
        {
            if (!realmComponent.ScenesAreFixed)
                return;

            if (!realmData.WorldManifest.IsEmpty)
            {
                NativeHashSet<int2> occupiedParcels = realmData.WorldManifest.GetOccupiedParcels();
                var pointersList = new List<int2>(occupiedParcels.Count);
                foreach (int2 parcel in occupiedParcels)
                    pointersList.Add(parcel);

                var listPromise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(new List<SceneEntityDefinition>(pointersList.Count), pointersList,
                        new CommonLoadingArguments("https://asset-bundle-registry.decentraland.zone/entities/active?world_name=pastrami.dcl.eth")), PartitionComponent.TOP_PRIORITY);

                World.Add(entity, new FixedScenePointers(listPromise));
                return;
            }

            // tolerate allocations as it's once per realm only
            var promises = new AssetPromise<SceneEntityDefinition, GetSceneDefinition>[realmComponent.Ipfs.SceneUrns.Count];

            for (var i = 0; i < realmComponent.Ipfs.SceneUrns.Count; i++)
            {
                string urn = realmComponent.Ipfs.SceneUrns[i];
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                // can't prioritize scenes definition - they are always top priority
                var promise = AssetPromise<SceneEntityDefinition, GetSceneDefinition>
                   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(realmComponent.Ipfs.ContentBaseUrl)), ipfsPath), PartitionComponent.TOP_PRIORITY);

                promises[i] = promise;
            }

            World.Add(entity, new FixedScenePointers(promises));
        }

        [Query]
        private void ResolvePromises(ref FixedScenePointers fixedScenePointers, ref ProcessedScenePointers processedScenePointers, ref RealmComponent realmComponent)
        {
            if (fixedScenePointers.AllPromisesResolved) return;

            if (fixedScenePointers.ListPromise is { } listPromise)
            {
                if (listPromise.IsConsumed) return;

                if (!listPromise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result))
                    return;

                fixedScenePointers.AllPromisesResolved = true;

                string spawnCoordinate = "-1,-1";

                if (realmData.WorldManifest.spawn_coordinate != null)
                    spawnCoordinate = $"{realmData.WorldManifest.spawn_coordinate.x},{realmData.WorldManifest.spawn_coordinate.y}";

                if (result.Succeeded)
                {
                    IReadOnlyList<SceneEntityDefinition> definitions = result.Asset.Value;
                    for (var i = 0; i < definitions.Count; i++)
                    {
                        SceneEntityDefinition definition = definitions[i];
                        var ipfsPath = new IpfsPath(definition.id, URLDomain.FromString("https://worlds-content-server.decentraland.zone/contents/"));
                        CreateSceneEntity(definition, ipfsPath);
                        IReadOnlyList<Vector2Int> parcels = definition.metadata.scene.DecodedParcels;
                        for (var j = 0; j < parcels.Count; j++)
                            processedScenePointers.Value.Add(parcels[j].ToInt2());

                        if (definition.pointers.ToList().Contains(spawnCoordinate))
                            fixedScenePointers.StartupScene = definition;
                    }
                }

                return;
            }

            fixedScenePointers.AllPromisesResolved = true;

            for (var i = 0; i < fixedScenePointers.Promises.Length; i++)
            {
                ref AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise = ref fixedScenePointers.Promises[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<SceneEntityDefinition> result))
                {
                    if (result.Succeeded)
                    {
                        CreateSceneEntity(result.Asset, promise.LoadingIntention.IpfsPath);
                        IReadOnlyList<Vector2Int> parcels = result.Asset.metadata.scene.DecodedParcels;

                        for (var j = 0; j < parcels.Count; j++)
                            processedScenePointers.Value.Add(parcels[j].ToInt2());
                    }
                }
                else
                {
                    // at least one unresolved promises
                    fixedScenePointers.AllPromisesResolved = false;
                }
            }
        }
    }
}
