using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utilities;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Utilities.Extensions;
using SceneRunner.Scene;
using System.Linq;
using GetSceneDefinition = ECS.SceneLifeCycle.SceneDefinition.GetSceneDefinition;

namespace Global.Dynamic
{
    public class PortableExperiencesController : IPortableExperiencesController
    {
        private static readonly QueryDescription GET_SCENE_DEFINITION = new QueryDescription().WithAll<GetSceneDefinition>();
        private static readonly QueryDescription SCENE_DEFINITION_COMPONENT = new QueryDescription().WithAll<SceneDefinitionComponent>();

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

        public async UniTask CreatePortableExperienceAsync(string ens, string urn, CancellationToken ct)
        {
            World world = globalWorld!.EcsWorld;

            //According to kernel implementation, the id value is used as an urn
            //https://github.com/decentraland/unity-renderer/blob/b3b170e404ec43bb8bc08ec1f6072812005ebad3/browser-interface/packages/shared/apis/host/PortableExperiences.ts#L28
            //And is validated first. As it has no ipfs config, it uses the one f

            string worldUrl = string.Empty;

            if (!string.IsNullOrEmpty(urn))
            {
                //TODO: Enable loading PX from urns using current scene realm data
                //worldUrl = IpfsHelper.ParseUrn(urn).BaseUrl.Value;
            }

            if (EnsUtils.ValidateEns(ens))
            {
                worldUrl = EnsUtils.ConvertEnsToWorldUrl(ens);
            }

            if (!worldUrl.IsValidUrl()) return; //Return with error to the JS side "('Invalid Spawn params. Provide a URN or an ENS name.')"

            URLDomain portableExperiencePath = URLDomain.FromString(worldUrl);
            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));

            await UniTask.SwitchToMainThread();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            try
            {
                //in case the url is wrong or any other potential issue with the request
                ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

                if (result.configurations.scenesUrn.Count == 0)
                {
                    //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                    return; //TODO: return error "Scene not available"
                }

                realmData.Reconfigure(
                    new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                    result.configurations.realmName.EnsureNotNull("Realm name not found"),
                    result.configurations.networkId,
                    result.comms?.adapter ?? string.Empty
                );

                var parentScene = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
                PortableExperienceEntities.Add(ens, world.Create(new PortableExperienceComponent(realmData, (parentScene != null? parentScene.Info.Name : "main"))));
            }
            catch (Exception e)
            {
                //TODO: Add proper exception handling
                //Return with error to the JS side if it fails "Error fetching scene"
                Console.WriteLine(e);
                throw;
            }
        }

        public bool CanKillPortableExperience(string ens)
        {
            if (globalWorld == null) return false;

            var currentSceneFacade = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
            if (currentSceneFacade == null) return false;

            World world = globalWorld.EcsWorld;

            if (PortableExperienceEntities.TryGetValue(ens, out var portableExperienceEntity))
            {
                var portableExperienceComponent = world.Get<PortableExperienceComponent>(portableExperienceEntity);
                return portableExperienceComponent.ParentSceneId == currentSceneFacade.Info.Name;
            }

            return false;
        }


        public async UniTask UnloadPortableExperienceAsync(string ens, CancellationToken ct)
        {
            if (globalWorld == null) return;

            if (!EnsUtils.ValidateEns(ens)) return; //Return error to JS side
            //TODO: We need to be able to kill PX using only the urn as well, it will mean some changes to this code.

            World world = globalWorld.EcsWorld;

            await UniTask.SwitchToMainThread();

            //We need to dispose the scene that the PX has created.
             if (PortableExperienceEntities.TryGetValue(ens, out var portableExperienceEntity))
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
                //and check if their IpfsPath.EntityId coincides with the scene's entityId and if so, delete them.
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
                PortableExperienceEntities.Remove(ens);

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
