using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadPortableExperiencePointersSystem : LoadScenePointerSystemBase
    {
        internal LoadPortableExperiencePointersSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResolvePromisesQuery(World);
            InitiateDefinitionLoadingQuery(World);
        }

        [Query]
        [None(typeof(PortableExperienceScenePointers))]
        private void InitiateDefinitionLoading(in Entity entity, ref PortableExperienceComponent portableExperienceComponent)
        {
            // tolerate allocations as it's once per realm only
            var promises = new AssetPromise<SceneEntityDefinition, GetSceneDefinition>[portableExperienceComponent.Ipfs.SceneUrns.Count];

            for (var i = 0; i < portableExperienceComponent.Ipfs.SceneUrns.Count; i++)
            {
                string urn = portableExperienceComponent.Ipfs.SceneUrns[i];
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                // can't prioritize scenes definition - they are always top priority
                var promise = AssetPromise<SceneEntityDefinition, GetSceneDefinition>
                   .Create(World, new GetSceneDefinition(new CommonLoadingArguments(ipfsPath.GetUrl(portableExperienceComponent.Ipfs.ContentBaseUrl)), ipfsPath), PartitionComponent.TOP_PRIORITY);

                promises[i] = promise;
            }

            World.Add(entity, new PortableExperienceScenePointers(promises));
        }

        [Query]
        private void ResolvePromises(ref PortableExperienceScenePointers portableExperienceScenePointers)
        {
            if (portableExperienceScenePointers.AllPromisesResolved) return;

            portableExperienceScenePointers.AllPromisesResolved = true;

            for (var i = 0; i < portableExperienceScenePointers.Promises.Length; i++)
            {
                ref AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise = ref portableExperienceScenePointers.Promises[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<SceneEntityDefinition> result))
                {
                    if (result.Succeeded)
                    {
                        CreateSceneEntity(result.Asset, promise.LoadingIntention.IpfsPath, true);
                    }
                }
                else
                {
                    // at least one unresolved promises
                    portableExperienceScenePointers.AllPromisesResolved = false;
                }
            }
        }
    }
}
