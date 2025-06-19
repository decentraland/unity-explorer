using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.FeatureFlags;
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
using Global.Dynamic;
using SceneRunner.Scene;
using System.Linq;
using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.Dynamic.LaunchModes;

namespace PortableExperiences.Controller
{
    public class ECSPortableExperiencesController : IPortableExperiencesController
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;
        private readonly List<IPortableExperiencesController.SpawnResponse> spawnResponsesList = new ();
        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource urlsSources;
        private GlobalWorld globalWorld;

        public Dictionary<ENS, Entity> PortableExperienceEntities { get; } = new ();

        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set => globalWorld = value;
        }

        private World world => globalWorld.EcsWorld;

        public ECSPortableExperiencesController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache,
            ILaunchMode launchMode,
            IDecentralandUrlsSource urlsSources)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
            this.launchMode = launchMode;
            this.urlsSources = urlsSources;
        }

        public async UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false)
        {
            if (!force)
                switch (isGlobalPortableExperience)
                {
                    //If it's not a Global PX and common PXs are disabled
                    case false when !FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE):
                        throw new Exception("Portable Experiences are disabled");

                    //If it IS a Global PX but Global PXs are disabled
                    case true when !FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE):
                        throw new Exception("Global Portable Experiences are disabled");
                }

            if (PortableExperienceEntities.ContainsKey(ens)) throw new Exception($"ENS {ens} is already loaded");

            string worldUrl = string.Empty;

            if (ens.IsValid)
                worldUrl = ens.ConvertEnsToWorldUrl();

            if (!worldUrl.IsValidUrl()) throw new ArgumentException($"Invalid Spawn params. Provide a valid ENS name {ens}");

            var portableExperiencePath = URLDomain.FromString(worldUrl);
            URLAddress url = portableExperiencePath.Append(new URLPath("/about"));

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

            var serverAbout = new ServerAbout();
            ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

            if (result.configurations.scenesUrn.Count == 0)

                //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                throw new Exception($"Scene not Available in provided Portable Experience with ens: {ens}");

            var assetBundleRegistry =
                FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK)
                    ? URLBuilder.Combine(URLDomain.FromString(urlsSources.Url(DecentralandUrl.AssetBundleRegistry)),
                        URLSubdirectory.FromString("entities/active"))
                    : URLDomain.EMPTY;

            var realmData = new RealmData();

            realmData.Reconfigure(
                new IpfsRealm(web3IdentityCache, webRequestController, portableExperiencePath, assetBundleRegistry,
                    result),
                result.configurations.realmName.EnsureNotNull("Realm name not found"),
                result.configurations.networkId,
                result.comms?.adapter ?? string.Empty,
                result.comms?.protocol ?? string.Empty,
                portableExperiencePath.Value,
                launchMode.CurrentMode is LaunchMode.LocalSceneDevelopment
            );

            ISceneFacade parentScene = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);
            string parentSceneName = parentScene != null ? parentScene.Info.Name : "main";
            Entity portableExperienceEntity = world.Create(new PortableExperienceRealmComponent(realmData, parentSceneName, isGlobalPortableExperience), new PortableExperienceComponent(ens));

            PortableExperienceEntities.Add(ens, portableExperienceEntity);

            return new IPortableExperiencesController.SpawnResponse
                { name = realmData.RealmName, ens = ens.ToString(), parent_cid = parentSceneName, pid = portableExperienceEntity.Id.ToString() };
        }

        public bool CanKillPortableExperience(ENS ens)
        {
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE)) return false;

            ISceneFacade currentSceneFacade = scenesCache.CurrentScene;
            if (currentSceneFacade == null) return false;

            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                PortableExperienceRealmComponent portableExperienceRealmComponent = world.Get<PortableExperienceRealmComponent>(portableExperienceEntity);

                if (portableExperienceRealmComponent.IsGlobalPortableExperience) return false;

                return portableExperienceRealmComponent.ParentSceneId == currentSceneFacade.Info.Name;
            }

            return false;
        }

        public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences()
        {
            spawnResponsesList.Clear();

            foreach (KeyValuePair<ENS, Entity> portableExperience in PortableExperienceEntities)
            {
                PortableExperienceRealmComponent pxRealmComponent = world.Get<PortableExperienceRealmComponent>(portableExperience.Value);

                spawnResponsesList.Add(new IPortableExperiencesController.SpawnResponse
                {
                    name = pxRealmComponent.RealmData.RealmName,
                    ens = portableExperience.Key.ToString(),
                    parent_cid = pxRealmComponent.ParentSceneId,
                    pid = portableExperience.Value.Id.ToString(),
                });
            }

            return spawnResponsesList;
        }

        public void UnloadAllPortableExperiences()
        {
            foreach (IPortableExperiencesController.SpawnResponse spawnResponse in GetAllPortableExperiences())
                UnloadPortableExperienceByEns(new ENS(spawnResponse.ens));
        }

        public IPortableExperiencesController.ExitResponse UnloadPortableExperienceByEns(ENS ens)
        {
            if (!ens.IsValid) throw new ArgumentException($"The provided ens {ens.ToString()} is invalid");

            if (PortableExperienceEntities.TryGetValue(ens, out Entity portableExperienceEntity))
            {
                world.Add<DeleteEntityIntention>(portableExperienceEntity);

                PortableExperienceEntities.Remove(ens);

                return new IPortableExperiencesController.ExitResponse { status = true };
            }

            return new IPortableExperiencesController.ExitResponse { status = false };
        }
    }
}
