using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.ParcelsService;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Utilities.Extensions;
using Unity.Mathematics;
using GetSceneDefinition = ECS.SceneLifeCycle.SceneDefinition.GetSceneDefinition;

namespace Global.Dynamic
{
    public class PortableExperiencesController : IPortableExperiencesController
    {
        // TODO it can be dangerous to clear the realm, instead we may destroy it fully and reconstruct but we will need to
        // TODO construct player/camera entities again and allocate more memory. Evaluate
        // Realms + Promises
        private static readonly QueryDescription GET_SCENE_DEFINITION = new QueryDescription().WithAll<GetSceneDefinition>();
        private static readonly QueryDescription SCENE_DEFINITION_COMPONENT = new QueryDescription().WithAll<SceneDefinitionComponent>();

        private readonly List<ISceneFacade> allScenes = new (PoolConstants.SCENES_COUNT);
        private readonly ServerAbout serverAbout = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;

        private GlobalWorld? globalWorld;
        private readonly RealmData realmData = new ();
        public Dictionary<string, Entity> PortableExperienceEntities { get; } = new ();
        private List<Entity> entitiesToDestroy = new List<Entity>();
        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set => globalWorld = value;
        }

        public PortableExperiencesController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
        }

        public async UniTask CreatePortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            await UniTask.SwitchToMainThread();

            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));
            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            try
            {
                //in case the url is wrong or any other potential issue with the request
                ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

                if (result.configurations.scenesUrn.Count == 0)
                {
                    //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                    return;
                }

                realmData.Reconfigure(
                    new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                    result.configurations.realmName.EnsureNotNull("Realm name not found"),
                    result.configurations.networkId,
                    result.comms?.adapter ?? string.Empty
                );

                PortableExperienceEntities.Add(portableExperiencePath.Value, world.Create(new PortableExperienceComponent(realmData)));
            }
            catch (Exception e)
            {
                //TODO: Add proper exception handling
                Console.WriteLine(e);
                throw;
            }
        }

        public async UniTask UnloadPortableExperienceAsync(string portableExperiencePath, CancellationToken ct)
        {
            if (globalWorld == null) return;

            World world = globalWorld.EcsWorld;

            await UniTask.SwitchToMainThread();

            //We need to dispose the scene that the PX has created.
             if (PortableExperienceEntities.TryGetValue(portableExperiencePath, out var portableExperienceEntity))
            {
                var portableExperienceComponent = world.Get<PortableExperienceComponent>(portableExperienceEntity);

                //Portable Experiences only have one scene
                string? sceneUrn = portableExperienceComponent.Ipfs.SceneUrns[0];
                string sceneEntityId = string.Empty;

                if (sceneUrn != null)
                {
                    sceneEntityId = IpfsHelper.ParseUrn(sceneUrn).EntityId;

                        if (scenesCache.TryGetPortableExperienceBySceneUrn(sceneEntityId, out var sceneFacade))
                        {
                            await sceneFacade.DisposeAsync();
                            scenesCache.RemovePortableExperienceFacade(sceneEntityId);
                        }
                }

                // Clear the world from everything connected to the current PX
                //for this we will need to go over all these entities in the query
                //and check if their entity Id coincides with the scene's entity Id and if so, delete them.
                entitiesToDestroy.Clear();

                if (!string.IsNullOrEmpty(sceneEntityId))
                {
                    GetEntitiesToDestroy(world, sceneEntityId, GET_SCENE_DEFINITION, CheckGetSceneDefinitions, ref entitiesToDestroy);
                    GetEntitiesToDestroy(world, sceneEntityId, SCENE_DEFINITION_COMPONENT, CheckSceneDefinitionComponents, ref entitiesToDestroy);
                    foreach (var entity in entitiesToDestroy)
                    {
                        world.Destroy(entity);
                    }
                }

                world.Destroy(portableExperienceEntity);
                PortableExperienceEntities.Remove(portableExperiencePath);

                GC.Collect();
            }
        }

        private void GetEntitiesToDestroy(World world, string url, QueryDescription queryDescription, Func<Chunk, string, Entity> iterationFunc, ref List<Entity> entities)
        {
            var query = world.Query(queryDescription);

            foreach (var chunk in query.GetChunkIterator())
            {
                Entity entity = iterationFunc.Invoke(chunk, url);
                if ( entity != Entity.Null)
                {
                    entities.Add(entity);
                }
            }
        }

        private Entity CheckGetSceneDefinitions(Chunk chunk, string url)
        {
            var first = chunk.GetFirst<GetSceneDefinition>();
            if (first.IpfsPath.EntityId == url)
            {
                return chunk.Entity(0);
            }
            return Entity.Null;
        }

        private Entity CheckSceneDefinitionComponents(Chunk chunk, string url)
        {
            var first = chunk.GetFirst<SceneDefinitionComponent>();
            if (first.IpfsPath.EntityId == url)
            {
                return chunk.Entity(0);
            }
            return Entity.Null;
        }

    }
}
