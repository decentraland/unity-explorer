using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Load scenes definitions from the predefined list of pointers that never changes
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadStaticPointersSystem : LoadScenePointerSystemBase
    {
        internal LoadStaticPointersSystem(World world, HashSet<Vector2Int> roadCoordinates, IRealmData realmData) : base(world, roadCoordinates, realmData) { }

        protected override void Update(float t)
        {
            ResolvePromiseQuery(World);
        }

        [Query]
        [None(typeof(FixedScenePointers))]
        private void ResolvePromise(ref RealmComponent realm, ref StaticScenePointers staticScenePointers)
        {
            if (staticScenePointers.Promise == null)
            {
                // start loading
                staticScenePointers.Promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(new List<SceneEntityDefinition>(staticScenePointers.Value.Count), staticScenePointers.Value,
                        new CommonLoadingArguments(realm.Ipfs.AssetBundleRegistry)), PartitionComponent.TOP_PRIORITY);
            }
            else
            {
                // finalize loading
                AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = staticScenePointers.Promise.Value;

                if (promise.IsConsumed) return;

                if (promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result) && result.Succeeded)
                {
                    for (var i = 0; i < result.Asset.Value.Count; i++)
                    {
                        SceneEntityDefinition definition = result.Asset.Value[i];
                        var path = new IpfsPath(definition.id, URLDomain.EMPTY);
                        CreateSceneEntity(definition, path);
                    }
                }

                // Right promise back - it is Nullable Value Type
                staticScenePointers.Promise = promise;
            }
        }
    }
}
