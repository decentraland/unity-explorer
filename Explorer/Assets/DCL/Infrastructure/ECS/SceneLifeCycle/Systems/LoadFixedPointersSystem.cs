using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
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
        private readonly IDecentralandUrlsSource urlsSource;

        internal LoadFixedPointersSystem(World world, IRealmData realmData, IDecentralandUrlsSource urlsSource) : base(world, new HashSet<Vector2Int>(), realmData)
        {
            this.urlsSource = urlsSource;
        }

        protected override void Update(float t)
        {
            ResolveCatalystPromisesQuery(World);
            ResolveRegistryPromisesQuery(World);
            InitiateDefinitionLoadingQuery(World);
        }

        [Query]
        [None(typeof(FixedScenePointers))]
        private void InitiateDefinitionLoading(in Entity entity, ref RealmComponent realmComponent)
        {
            if (!realmComponent.ScenesAreFixed)
                return;

            if (realmData.WorldManifest.IsEmpty)
                InitiateCatalystPromise(entity, realmComponent);
            else
                InitiateRegistryPromise(entity);
        }

         [Query]
         [None(typeof(FixedPointerRegistryPromise))]
        private void ResolveCatalystPromises(ref FixedScenePointers fixedScenePointers, ref ProcessedScenePointers processedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved) return;

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

                        //We are gonna load into the first loaded scene as startup
                        fixedScenePointers.SceneResults.Add(result.Asset);

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


        [Query]
        [All(typeof(FixedPointerRegistryPromise))]
        private void ResolveRegistryPromises(ref FixedScenePointers fixedScenePointers, ref ProcessedScenePointers processedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved) return;

            if (fixedScenePointers.ListPromise!.Value.IsConsumed) return;

            if (!fixedScenePointers.ListPromise!.Value.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result))
                return;

            fixedScenePointers.AllPromisesResolved = true;

            if (result.Succeeded)
            {
                IReadOnlyList<SceneEntityDefinition> definitions = result.Asset.Value;
                for (var i = 0; i < definitions.Count; i++)
                {
                    SceneEntityDefinition definition = definitions[i];
                    fixedScenePointers.SceneResults.Add(definition);
                    var ipfsPath = new IpfsPath(definition.id, URLDomain.FromString(urlsSource.Url(DecentralandUrl.WorldContentServer)));
                    CreateSceneEntity(definition, ipfsPath);
                    IReadOnlyList<Vector2Int> parcels = definition.metadata.scene.DecodedParcels;

                    for (var j = 0; j < parcels.Count; j++)
                        processedScenePointers.Value.Add(parcels[j].ToInt2());


                }
            }
        }

        private void InitiateCatalystPromise(Entity entity, RealmComponent realmComponent)
        {
            // tolerate allocations as it's once per realm only
            var promises = new AssetPromise<SceneEntityDefinition, GetSceneDefinition>[realmComponent.Ipfs.SceneUrns.Count];

            for (var i = 0; i < realmComponent.Ipfs.SceneUrns.Count; i++)
            {
                string urn = realmComponent.Ipfs.SceneUrns[i];
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                // can't prioritize scenes definition - they are always top priority
                var promise = AssetPromise<SceneEntityDefinition, GetSceneDefinition>
                   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(URLDomain.FromString(urlsSource.Url(DecentralandUrl.WorldContentServer)))), ipfsPath), PartitionComponent.TOP_PRIORITY);

                promises[i] = promise;
            }

            World.Add(entity, new FixedScenePointers(promises));
        }

        private void InitiateRegistryPromise(Entity entity)
        {
            NativeHashSet<int2> occupiedParcels = realmData.WorldManifest.GetOccupiedParcels();
            var pointersList = new List<int2>(occupiedParcels.Count);
            foreach (int2 parcel in occupiedParcels)
                pointersList.Add(parcel);

            URLAddress destination = URLDomain.FromString(string.Format(urlsSource.Url(DecentralandUrl.WorldEntitiesActive), realmData.RealmName));

            var listPromise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                new GetSceneDefinitionList(new List<SceneEntityDefinition>(pointersList.Count), pointersList, new CommonLoadingArguments(destination)),
                PartitionComponent.TOP_PRIORITY);

            World.Add(entity, new FixedScenePointers(listPromise), new FixedPointerRegistryPromise());
        }

        //Flag structure for clear querys
        internal struct FixedPointerRegistryPromise
        {

        }

    }
}
