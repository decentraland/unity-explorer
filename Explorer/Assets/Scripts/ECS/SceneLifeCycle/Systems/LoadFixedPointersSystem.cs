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
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Loads scene definition from fixed scene pointers if the realm provides them
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(CreateEmptyPointersInFixedRealmSystem))] // we must execute it after to complete the split job, otherwise we write in a collection that is used by it
    public partial class LoadFixedPointersSystem : LoadScenePointerSystemBase
    {
        internal LoadFixedPointersSystem(World world) : base(world) { }

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

            // tolerate allocations as it's once per realm only
            var urnScenePromises = new AssetPromise<SceneEntityDefinition, GetSceneDefinition>[realmComponent.Ipfs.SceneUrns.Count];
            for (var i = 0; i < realmComponent.Ipfs.SceneUrns.Count; i++)
            {
                string urn = realmComponent.Ipfs.SceneUrns[i];
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                // can't prioritize scenes definition - they are always top priority
                var promise = AssetPromise<SceneEntityDefinition, GetSceneDefinition>
                   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(realmComponent.Ipfs.ContentBaseUrl)), ipfsPath), PartitionComponent.TOP_PRIORITY);

                urnScenePromises[i] = promise;
            }

            AssetPromise<SceneDefinitions, GetSceneDefinitionList>? pointerScenesPromise = null;
            if (realmComponent.RealmData.LocalSceneParcels is { Count: > 0 })
            {
                pointerScenesPromise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(new List<SceneEntityDefinition>(), realmComponent.RealmData.LocalSceneParcels,
                        new CommonLoadingArguments(realmComponent.RealmData.Ipfs.EntitiesActiveEndpoint)), PartitionComponent.TOP_PRIORITY);
            }

            World.Add(entity, new FixedScenePointers(urnScenePromises, pointerScenesPromise));
        }

        [Query]
        private void ResolvePromises(ref FixedScenePointers fixedScenePointers, ref ProcessedScenePointers processedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved) return;

            fixedScenePointers.AllPromisesResolved = true;

            if (fixedScenePointers.URNScenePromises is { Length: > 0 })
            {
                for (var i = 0; i < fixedScenePointers.URNScenePromises.Length; i++)
                {
                    ref AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise = ref fixedScenePointers.URNScenePromises[i];
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

            if (fixedScenePointers.PointerScenesPromise is { IsConsumed: false })
            {
                if (fixedScenePointers.PointerScenesPromise.Value.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result))
                {
                    if (result.Succeeded)
                    {
                        for (var i = 0; i < result.Asset.Value.Count; i++)
                        {
                            SceneEntityDefinition definition = result.Asset.Value[i];
                            var path = new IpfsPath(definition.id, URLDomain.EMPTY);
                            CreateSceneEntity(definition, path);

                            IReadOnlyList<Vector2Int> parcels = definition.metadata.scene.DecodedParcels;
                            for (var j = 0; j < parcels.Count; j++)
                                processedScenePointers.Value.Add(parcels[j].ToInt2());
                        }
                    }
                }
                else
                {
                    fixedScenePointers.AllPromisesResolved = false;
                }
            }
        }
    }
}
