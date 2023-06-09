﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Ipfs;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Loads scene definition from fixed scene pointers if the realm provides them
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
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
            var promises = new AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[realmComponent.Ipfs.SceneUrns.Count];

            for (var i = 0; i < realmComponent.Ipfs.SceneUrns.Count; i++)
            {
                string urn = realmComponent.Ipfs.SceneUrns[i];
                IpfsTypes.IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                var promise = AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
                   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(realmComponent.Ipfs.ContentBaseUrl)), ipfsPath));

                promises[i] = promise;
            }

            World.Add(entity, new FixedScenePointers(promises));
        }

        [Query]
        private void ResolvePromises(ref FixedScenePointers fixedScenePointers)
        {
            if (fixedScenePointers.AllPromisesResolved) return;

            fixedScenePointers.AllPromisesResolved = true;

            for (var i = 0; i < fixedScenePointers.Promises.Length; i++)
            {
                ref AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition> promise = ref fixedScenePointers.Promises[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<IpfsTypes.SceneEntityDefinition> result))
                {
                    if (result.Succeeded)
                        CreateSceneEntity(result.Asset, promise.LoadingIntention.IpfsPath, out _);
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
