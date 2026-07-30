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
using ECS.SceneLifeCycle.Realm;
using DCL.Utility;
using MVC;
using SceneRuntime.ScenePermissions;

namespace PortableExperiences.Controller
{
    public class ECSPortableExperiencesController : IPortableExperiencesController
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;
        private readonly LocalPortableExperienceCache localPortableExperienceCache;
        private readonly List<IPortableExperiencesController.SpawnResponse> spawnResponsesList = new ();
        private readonly HashSet<string> loadingPortableExperiences = new ();
        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource urlsSources;
        private GlobalWorld globalWorld;

        public Dictionary<string, Entity> PortableExperienceEntities { get; } = new ();

        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set => globalWorld = value;
        }

        public IMVCManager? MvcManager { get; set; }

        private World world => globalWorld.EcsWorld;

        public event Action<string> PortableExperienceLoaded;
        public event Action<string> PortableExperienceUnloaded;

        public ECSPortableExperiencesController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache,
            LocalPortableExperienceCache localPortableExperienceCache,
            ILaunchMode launchMode,
            IDecentralandUrlsSource urlsSources)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
            this.localPortableExperienceCache = localPortableExperienceCache;
            this.launchMode = launchMode;
            this.urlsSources = urlsSources;

            // The controller lives for the whole application lifetime, so the subscription is never torn down.
            web3IdentityCache.OnIdentityCleared += localPortableExperienceCache.Clear;
        }

        public async UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false)
        {
            ISceneFacade? parentScene = scenesCache.Scenes.FirstOrDefault(s => s.SceneStateProvider.IsCurrent);

            if (!force)
                switch (isGlobalPortableExperience)
                {
                    //If it's not a Global PX and common PXs are disabled
                    case false when !FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE):
                        throw new Exception("Portable Experiences are disabled");

                    //If it IS a Global PX but Global PXs are disabled
                    case true when !FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE):
                        throw new Exception("Global Portable Experiences are disabled");

                    //If it's a local PX (not Global) but the requesting scene does not have permissions to spawn PXs
                    case false when parentScene != null && !parentScene.SceneData.SceneEntityDefinition.metadata.requiredPermissions.Contains(ScenePermissionNames.PORTABLE_EXPERIENCE):
                        throw new Exception($"The parent scene {parentScene.Info.Name} is trying to spawn a portable experience but lacks the '{ScenePermissionNames.PORTABLE_EXPERIENCE}' permission.");
                }

            var portableExperienceId = ens.ToString();

            if (PortableExperienceEntities.TryGetValue(portableExperienceId, out Entity existingEntity))
            {
                ReportHub.LogWarning(ReportCategory.PORTABLE_EXPERIENCE, $"ENS {ens} is already loaded, returning the existing Portable Experience");

                PortableExperienceMetadata existingMetadata = world.Get<PortableExperienceMetadata>(existingEntity);

                return new IPortableExperiencesController.SpawnResponse
                    { name = existingMetadata.Name, ens = existingMetadata.Ens, parent_cid = existingMetadata.ParentSceneId, pid = existingMetadata.Id };
            }

            if (!loadingPortableExperiences.Add(portableExperienceId)) throw new Exception($"ENS {ens} is already being loaded");

            try
            {
                string worldUrl = string.Empty;

                if (ens.IsValid)
                    worldUrl = ens.ConvertEnsToWorldUrl(urlsSources.Url(DecentralandUrl.WorldServer));

                if (!worldUrl.IsValidUrl()) throw new ArgumentException($"Invalid Spawn params. Provide a valid ENS name {ens}");

                var portableExperiencePath = URLDomain.FromString(worldUrl);
                URLAddress url = portableExperiencePath.Append(new URLPath("/about"));

                GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> genericGetRequest = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);

                var serverAbout = new ServerAbout();
                ServerAbout result = await genericGetRequest.OverwriteFromJsonAsync(serverAbout, WRJsonParser.Unity);

                if (result.configurations.scenesUrn.Count == 0)
                    //The loaded realm does not have any fixed scene, so it cannot be loaded as a Portable Experience
                    throw new Exception($"Scene not Available in provided Portable Experience with ens: {ens}");

                var ipfsRealm = new IpfsRealm(portableExperiencePath, result);

                if (!force && !isGlobalPortableExperience)
                {
                    string portableExperienceName = string.IsNullOrEmpty(result.configurations.realmName) ? portableExperienceId : result.configurations.realmName;
                    await EnsureAuthorizedByUserAsync(portableExperienceId, portableExperienceName, ipfsRealm, ct);
                }

                var realmData = new RealmData();

                realmData.Reconfigure(
                    ipfsRealm,
                    result.configurations.realmName.EnsureNotNull("Realm name not found"),
                    result.configurations.networkId,
                    result.comms?.adapter ?? string.Empty,
                    result.comms?.protocol ?? string.Empty,
                    portableExperiencePath.Value,
                    launchMode.CurrentMode is LaunchMode.LocalSceneDevelopment,
                    WorldManifest.Empty
                );

                string parentSceneName = parentScene?.Info.Name ?? "main";
                Entity portableExperienceEntity = world.Create();
                world.Add(portableExperienceEntity, new PortableExperienceRealmComponent(realmData, parentSceneName, isGlobalPortableExperience), new PortableExperienceComponent(ens));
                world.Add(portableExperienceEntity, new PortableExperienceMetadata
                {
                    Type = isGlobalPortableExperience ? PortableExperienceType.Global : PortableExperienceType.Local,
                    Ens = portableExperienceId,
                    Id = portableExperienceEntity.Id.ToString(),
                    Name = realmData.RealmName,
                    ParentSceneId = parentSceneName
                });

                PortableExperienceEntities.Add(portableExperienceId, portableExperienceEntity);

                PortableExperienceLoaded?.Invoke(portableExperienceId);

                return new IPortableExperiencesController.SpawnResponse
                    { name = realmData.RealmName, ens = portableExperienceId, parent_cid = parentSceneName, pid = portableExperienceEntity.Id.ToString() };
            }
            finally
            {
                loadingPortableExperiences.Remove(portableExperienceId);
            }
        }

        private async UniTask EnsureAuthorizedByUserAsync(string portableExperienceId, string portableExperienceName, IIpfsRealm ipfsRealm, CancellationToken ct)
        {
            if (localPortableExperienceCache.AuthorizedPortableExperiences.Contains(portableExperienceId)) return;

            if (localPortableExperienceCache.DeniedPortableExperiences.Contains(portableExperienceId))
                throw new Exception($"The user has denied authorization for the portable experience '{portableExperienceId}' in this session.");

            IReadOnlyList<string> permissions = await localPortableExperienceCache.GetPermissionsRequiringAuthorizationAsync(portableExperienceId, ipfsRealm, ct);

            if (permissions.Count == 0)
            {
                localPortableExperienceCache.AuthorizedPortableExperiences.Add(portableExperienceId);
                return;
            }

            // Fail closed: a portable experience that requires permissions must never spawn without explicit consent.
            if (MvcManager == null)
            {
                ReportHub.LogError(ReportCategory.PORTABLE_EXPERIENCE, $"Cannot request authorization for portable experience '{portableExperienceId}': UI is not initialized yet.");
                throw new Exception($"Portable experience '{portableExperienceId}' requires user authorization but the UI is not available yet.");
            }

            bool authorized = await PortableExperienceAuthorizationPopupController.RequestAuthorizationAsync(MvcManager, portableExperienceName, permissions, ct);

            // A cancelled request must not be recorded as a user decision.
            ct.ThrowIfCancellationRequested();

            if (!authorized)
            {
                localPortableExperienceCache.DeniedPortableExperiences.Add(portableExperienceId);
                throw new Exception($"The user denied the portable experience '{portableExperienceName}'.");
            }

            localPortableExperienceCache.AuthorizedPortableExperiences.Add(portableExperienceId);
        }

        public bool CanKillPortableExperience(string id)
        {
            if (!PortableExperienceEntities.TryGetValue(id, out Entity portableExperienceEntity)) return false;

            PortableExperienceMetadata metadata = world.Get<PortableExperienceMetadata>(portableExperienceEntity);

            switch (metadata.Type)
            {
                case PortableExperienceType.Global:
                    // Cannot kill a Global PX ever
                    return false;

                case PortableExperienceType.Local:
                    if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE)) return false;

                    ISceneFacade currentSceneFacade = scenesCache.CurrentScene.Value;
                    return currentSceneFacade != null && metadata.ParentSceneId == currentSceneFacade.Info.Name;

                case PortableExperienceType.SmartWearable:
                    // Can always kill a Smart Wearable PX
                    return true;
            }

            throw new InvalidOperationException();
        }

        public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences()
        {
            spawnResponsesList.Clear();

            foreach ((string _, Entity px) in PortableExperienceEntities)
            {
                PortableExperienceMetadata metadata = world.Get<PortableExperienceMetadata>(px);

                spawnResponsesList.Add(new IPortableExperiencesController.SpawnResponse
                {
                    ens = metadata.Ens,
                    pid = metadata.Id,
                    name = metadata.Name,
                    parent_cid = metadata.ParentSceneId
                });
            }

            return spawnResponsesList;
        }

        public void UnloadAllPortableExperiences()
        {
            foreach (IPortableExperiencesController.SpawnResponse spawnResponse in GetAllPortableExperiences())
                UnloadPortableExperienceById(spawnResponse.ens);
        }

        public void AddPortableExperience(string id, Entity portableExperience)
        {
            PortableExperienceEntities.TryAdd(id, portableExperience);
        }

        public IPortableExperiencesController.ExitResponse UnloadPortableExperienceById(string id)
        {
            if (PortableExperienceEntities.TryGetValue(id, out Entity portableExperienceEntity))
            {
                world.Add<DeleteEntityIntention>(portableExperienceEntity);

                PortableExperienceEntities.Remove(id);

                PortableExperienceUnloaded?.Invoke(id);

                return new IPortableExperiencesController.ExitResponse { status = true };
            }

            return new IPortableExperiencesController.ExitResponse { status = false };
        }
    }
}
