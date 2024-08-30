using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Utilities.Extensions;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System.Linq;

namespace PortableExperiences.Controller
{
    public class ECSPortableExperiencesController : IPortableExperiencesController
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;
        private readonly World globalWorld;
        private readonly List<IPortableExperiencesController.SpawnResponse> spawnResponsesList = new ();
        private readonly ServerAbout serverAbout = new ();
        public Dictionary<ENS, Entity> PortableExperienceEntities { get; } = new ();


        public ECSPortableExperiencesController(
            World globalWorld,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache)
        {
            this.globalWorld = globalWorld;
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
        }

        public async UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false)
        {
            string worldUrl = string.Empty;

            if (ens.IsValid) { worldUrl = ens.ConvertEnsToWorldUrl(); }

            if (!worldUrl.IsValidUrl()) throw new ArgumentException("Invalid Spawn params. Provide a valid ENS name");

            var portableExperiencePath = URLDomain.FromString(worldUrl);
            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            if (result.configurations.scenesUrn.Count == 0)
            {
                //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                throw new Exception($"Scene not Available in provided Portable Experience with ens: {ens}");
            }
            var realmData = new RealmData();
            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty,
                result.comms?.protocol ?? string.Empty,
                portableExperiencePath.Value
            );

            ISceneFacade parentScene = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
            string parentSceneName = parentScene != null ? parentScene.Info.Name : "main";
            Entity portableExperienceEntity = globalWorld.Create(new PortableExperienceRealmComponent(realmData, parentSceneName, isGlobalPortableExperience), new PortableExperienceComponent(ens));
            PortableExperienceEntities.Add(ens, portableExperienceEntity);

            return new IPortableExperiencesController.SpawnResponse
                { name = realmData.RealmName, ens = ens.ToString(), parent_cid = parentSceneName, pid = portableExperienceEntity.Id.ToString() };
        }

        public bool CanKillPortableExperience(ENS ens)
        {
            ISceneFacade currentSceneFacade = scenesCache.CurrentScene;
            if (currentSceneFacade == null) return false;

            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                PortableExperienceRealmComponent portableExperienceRealmComponent = globalWorld.Get<PortableExperienceRealmComponent>(portableExperienceEntity);

                if (portableExperienceRealmComponent.IsGlobalPortableExperience) return false;

                return portableExperienceRealmComponent.ParentSceneId == currentSceneFacade.Info.Name;
            }

            return false;
        }

        public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences()
        {
            spawnResponsesList.Clear();

            foreach (var portableExperience in PortableExperienceEntities)
            {
                PortableExperienceRealmComponent pxRealmComponent = globalWorld.Get<PortableExperienceRealmComponent>(portableExperience.Value);

                spawnResponsesList.Add(new IPortableExperiencesController.SpawnResponse {
                        name = pxRealmComponent.RealmData.RealmName,
                        ens = portableExperience.Key.ToString(),
                        parent_cid = pxRealmComponent.ParentSceneId,
                        pid = portableExperience.Value.Id.ToString() });
            }

            return spawnResponsesList;
        }

        public IPortableExperiencesController.ExitResponse UnloadPortableExperienceByEns(ENS ens)
        {
            if (!ens.IsValid) throw new ArgumentException($"The provided ens {ens.ToString()} is invalid");

            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                globalWorld.Add<DeleteEntityIntention>(portableExperienceEntity);

                PortableExperienceEntities.Remove(ens);

                return new IPortableExperiencesController.ExitResponse { status = true };
            }

            return new IPortableExperiencesController.ExitResponse { status = false };
        }
    }
}
