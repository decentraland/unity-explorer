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
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System.Linq;
using GetSceneDefinition = ECS.SceneLifeCycle.SceneDefinition.GetSceneDefinition;

namespace PortableExperiences.Controller
{
    public class PortableExperiencesController : IPortableExperiencesController
    {
        private static readonly QueryDescription GET_SCENE_DEFINITION = new QueryDescription().WithAll<GetSceneDefinition>();
        private static readonly QueryDescription SCENE_DEFINITION_COMPONENT = new QueryDescription().WithAll<SceneDefinitionComponent>();

        private readonly ServerAbout serverAbout = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;

        private readonly ObjectProxy<World> globalWorldProxy;
        private List<Entity> entitiesToDestroy = new ();
        public Dictionary<ENS, Entity> PortableExperienceEntities { get; } = new ();
        private World world => globalWorldProxy.Object;
        private List<IPortableExperiencesApi.SpawnResponse> spawnResponsesList = new ();

        public PortableExperiencesController(
            ObjectProxy<World> world,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache)
        {
            globalWorldProxy = world;
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
        }

        public async UniTask<IPortableExperiencesApi.SpawnResponse> CreatePortableExperienceAsync(ENS ens, URN urn, CancellationToken ct, bool isGlobalPortableExperience = false)
        {
            //According to kernel implementation, the id value is used as an urn
            //https://github.com/decentraland/unity-renderer/blob/b3b170e404ec43bb8bc08ec1f6072812005ebad3/browser-interface/packages/shared/apis/host/PortableExperiences.ts#L28
            //And is validated first. As it has no ipfs config, it uses the one from the current realm apparently

            string worldUrl = string.Empty;

            if (!string.IsNullOrEmpty(urn))
            {
                //TODO: Enable loading PX from urns using current scene realm data. -> will be done in next iteration.
                //worldUrl = IpfsHelper.ParseUrn(urn).BaseUrl.Value;
            }

            if (ens.IsValid) { worldUrl = ENSUtils.ConvertEnsToWorldUrl(ens); }

            if (!worldUrl.IsValidUrl()) throw new ArgumentException("Invalid Spawn params. Provide a URN or an ENS name");

            var portableExperiencePath = URLDomain.FromString(worldUrl);
            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));


            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            if (result.configurations.scenesUrn.Count == 0)
            {
                //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                throw new Exception($"Scene not Available in provided Portable Experience with ens:{ens} - urn: {urn}");
            }
            var realmData = new RealmData();
            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty,
                result.comms?.protocol ?? string.Empty
            );

            ISceneFacade parentScene = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
            string parentSceneName = parentScene != null ? parentScene.Info.Name : "main";
            Entity portableExperienceEntity = world.Create(new PortableExperienceRealmComponent(realmData, parentSceneName, isGlobalPortableExperience));
            PortableExperienceEntities.Add(ens, portableExperienceEntity);

            return new IPortableExperiencesApi.SpawnResponse
                { name = realmData.RealmName, ens = ens.ToString(), parent_cid = parentSceneName, pid = portableExperienceEntity.Id.ToString() };
        }

        public bool CanKillPortableExperience(ENS ens)
        {
            ISceneFacade currentSceneFacade = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
            if (currentSceneFacade == null) return false;

            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                PortableExperienceRealmComponent portableExperienceRealmComponent = world.Get<PortableExperienceRealmComponent>(portableExperienceEntity);
                return portableExperienceRealmComponent.ParentSceneId == currentSceneFacade.Info.Name;
            }

            return false;
        }

        public List<IPortableExperiencesApi.SpawnResponse> GetAllPortableExperiences()
        {
            spawnResponsesList.Clear();

            foreach (var portableExperience in PortableExperienceEntities)
            {
                PortableExperienceRealmComponent pxRealmComponent = world.Get<PortableExperienceRealmComponent>(portableExperience.Value);

                spawnResponsesList.Add(new IPortableExperiencesApi.SpawnResponse {
                        name = pxRealmComponent.RealmData.RealmName,
                        ens = portableExperience.Key.ToString(),
                        parent_cid = pxRealmComponent.ParentSceneId,
                        pid = portableExperience.Value.Id.ToString() });
            }

            return spawnResponsesList;
        }

        public async UniTask<IPortableExperiencesApi.ExitResponse> UnloadPortableExperienceAsync(ENS ens, CancellationToken ct)
        {
            if (ens.IsValid) throw new ArgumentException($"The provided ens {ens.ToString()} is invalid");

            //TODO: We need to be able to kill PX using only the urn as well, it will mean some changes to this code, this will be done in the next iteration.

            //We need to dispose the scene that the PX has created.
            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                PortableExperienceRealmComponent portableExperienceRealmComponent = world.Get<PortableExperienceRealmComponent>(portableExperienceEntity);

                //Portable Experiences only have one scene
                string? sceneUrn = portableExperienceRealmComponent.Ipfs.SceneUrns[0];
                string sceneEntityId = string.Empty;

                if (sceneUrn != null)
                {
                    sceneEntityId = IpfsHelper.ParseUrn(sceneUrn).EntityId;

                    if (scenesCache.TryGetPortableExperienceBySceneUrn(sceneEntityId, out ISceneFacade sceneFacade))
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
                    GetEntitiesToDestroy(sceneEntityId, GET_SCENE_DEFINITION, CheckGetSceneDefinitions, ref entitiesToDestroy);
                    GetEntitiesToDestroy(sceneEntityId, SCENE_DEFINITION_COMPONENT, CheckSceneDefinitionComponents, ref entitiesToDestroy);

                    foreach (Entity entity in entitiesToDestroy) { world.Destroy(entity); }
                }

                world.Destroy(portableExperienceEntity);
                PortableExperienceEntities.Remove(ens);

                return new IPortableExperiencesApi.ExitResponse
                    { status = true };
            }

            return new IPortableExperiencesApi.ExitResponse
                { status = false };
        }

        private void GetEntitiesToDestroy(string url, QueryDescription queryDescription, Func<Chunk, string, Entity> iterationFunc, ref List<Entity> entities)
        {
            Query query = world.Query(queryDescription);

            foreach (Chunk chunk in query.GetChunkIterator())
            {
                Entity entity = iterationFunc.Invoke(chunk, url);

                if (entity != Entity.Null) { entities.Add(entity); }
            }
        }

        private Entity CheckGetSceneDefinitions(Chunk chunk, string url)
        {
            GetSceneDefinition first = chunk.GetFirst<GetSceneDefinition>();

            if (first.IpfsPath.EntityId == url) { return chunk.Entity(0); }

            return Entity.Null;
        }

        private Entity CheckSceneDefinitionComponents(Chunk chunk, string url)
        {
            SceneDefinitionComponent first = chunk.GetFirst<SceneDefinitionComponent>();

            if (first.IpfsPath.EntityId == url) { return chunk.Entity(0); }

            return Entity.Null;
        }
    }
}
