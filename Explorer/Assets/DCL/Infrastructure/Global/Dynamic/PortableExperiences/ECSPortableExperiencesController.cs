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
using DCL.Utility;
using Runtime.Wearables;
using SceneRuntime.ScenePermissions;

namespace PortableExperiences.Controller
{
    public class ECSPortableExperiencesController : IPortableExperiencesController
    {
        private const int MAX_PORTABLE_EXPERIENCES_PER_SCENE = 10;

        private readonly IWebRequestController webRequestController;
        private readonly IScenesCache scenesCache;
        private readonly LocalPortableExperienceCache localPortableExperienceCache;
        private readonly GlobalPortableExperienceCache globalPortableExperienceCache;
        private readonly SmartWearableCache smartWearableCache;
        private readonly List<IPortableExperiencesController.SpawnResponse> spawnResponsesList = new ();
        private readonly HashSet<string> loadingPortableExperiences = new ();
        private readonly Dictionary<string, int> localPortableExperiencesPerScene = new ();
        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource urlsSources;
        private GlobalWorld? globalWorld;

        public Dictionary<string, Entity> PortableExperienceEntities { get; } = new ();

        public GlobalWorld GlobalWorld
        {
            get => globalWorld.EnsureNotNull("GlobalWorld in RealmController is null");

            set => globalWorld = value;
        }

        public IPortableExperienceAuthorizationHandler? AuthorizationHandler { get; set; }

        private World world => GlobalWorld.EcsWorld;

        public event Action<string>? PortableExperienceLoaded;
        public event Action<string>? PortableExperienceUnloaded;

        public ECSPortableExperiencesController(
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache,
            LocalPortableExperienceCache localPortableExperienceCache,
            GlobalPortableExperienceCache globalPortableExperienceCache,
            SmartWearableCache smartWearableCache,
            ILaunchMode launchMode,
            IDecentralandUrlsSource urlsSources)
        {
            this.webRequestController = webRequestController;
            this.scenesCache = scenesCache;
            this.localPortableExperienceCache = localPortableExperienceCache;
            this.globalPortableExperienceCache = globalPortableExperienceCache;
            this.smartWearableCache = smartWearableCache;
            this.launchMode = launchMode;
            this.urlsSources = urlsSources;

            // The controller lives for the whole application lifetime, so the subscriptions are never torn down.
            web3IdentityCache.OnIdentityCleared += localPortableExperienceCache.Clear;
            web3IdentityCache.OnIdentityCleared += globalPortableExperienceCache.Clear;
        }

        public async UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false, bool requireUserAuthorization = false)
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

                    case false when parentScene != null && !parentScene.SceneData.SceneEntityDefinition.metadata.requiredPermissions.Contains(ScenePermissionNames.SPAWN_PORTABLE_EXPERIENCE):
                        throw new Exception($"The parent scene {parentScene.Info.Name} is trying to spawn a portable experience but lacks the '{ScenePermissionNames.SPAWN_PORTABLE_EXPERIENCE}' permission.");
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
                string parentSceneName = parentScene?.Info.Name ?? "main";

                bool isSceneSpawned = !force && !isGlobalPortableExperience;

                if (isSceneSpawned || requireUserAuthorization)
                {
                    if (isSceneSpawned)
                        EnsureSceneSpawnCapacity(parentSceneName);

                    string portableExperienceName = string.IsNullOrEmpty(result.configurations.realmName) ? portableExperienceId : result.configurations.realmName;
                    await EnsureAuthorizedByUserAsync(portableExperienceId, portableExperienceName, ipfsRealm, ct);

                    // Re-checked: concurrent spawns may have consumed the remaining capacity while awaiting.
                    if (isSceneSpawned)
                        EnsureSceneSpawnCapacity(parentSceneName);
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

                if (isGlobalPortableExperience)
                {
                    // A re-spawned Portable Experience must not stay marked as killed.
                    globalPortableExperienceCache.KilledPortableExperiences.Remove(portableExperienceId);
                    globalPortableExperienceCache.RunningPortableExperiences.Add(portableExperienceId);
                }
                else
                {
                    localPortableExperienceCache.KilledPortableExperiences.Remove(portableExperienceId);
                    localPortableExperienceCache.RunningPortableExperiences.Add(portableExperienceId);

                    localPortableExperiencesPerScene.TryGetValue(parentSceneName, out int count);
                    localPortableExperiencesPerScene[parentSceneName] = count + 1;
                }

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
                throw new PortableExperienceAuthorizationDeniedException($"The user has denied authorization for the portable experience '{portableExperienceId}' in this session.");

            IReadOnlyList<string> permissions = await localPortableExperienceCache.GetPermissionsRequiringAuthorizationAsync(portableExperienceId, ipfsRealm, ct);

            if (permissions.Count == 0)
            {
                localPortableExperienceCache.AuthorizedPortableExperiences.Add(portableExperienceId);
                return;
            }

            IPortableExperienceAuthorizationHandler? authorizationHandler = AuthorizationHandler;

            // Fail closed: a portable experience that requires permissions must never spawn without explicit consent.
            if (authorizationHandler == null)
            {
                ReportHub.LogError(ReportCategory.PORTABLE_EXPERIENCE, $"Cannot request authorization for portable experience '{portableExperienceId}': UI is not initialized yet.");
                throw new Exception($"Portable experience '{portableExperienceId}' requires user authorization but the UI is not available yet.");
            }

            bool authorized = await authorizationHandler.RequestAuthorizationAsync(portableExperienceName, permissions, ct);

            // A cancelled request must not be recorded as a user decision.
            ct.ThrowIfCancellationRequested();

            if (!authorized)
            {
                localPortableExperienceCache.DeniedPortableExperiences.Add(portableExperienceId);
                throw new PortableExperienceAuthorizationDeniedException($"The user denied the portable experience '{portableExperienceName}'.");
            }

            localPortableExperienceCache.AuthorizedPortableExperiences.Add(portableExperienceId);
        }

        private void EnsureSceneSpawnCapacity(string parentSceneName)
        {
            if (localPortableExperiencesPerScene.TryGetValue(parentSceneName, out int count) && count >= MAX_PORTABLE_EXPERIENCES_PER_SCENE)
                throw new Exception($"The scene '{parentSceneName}' has reached the maximum number of portable experiences it can spawn ({MAX_PORTABLE_EXPERIENCES_PER_SCENE}).");
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

                    ISceneFacade? currentSceneFacade = scenesCache.CurrentScene.Value;
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
            if (PortableExperienceEntities.TryAdd(id, portableExperience))
                PortableExperienceLoaded?.Invoke(id);
        }

        public IPortableExperiencesController.ExitResponse UnloadPortableExperienceById(string id)
        {
            if (PortableExperienceEntities.TryGetValue(id, out Entity portableExperienceEntity))
            {
                PortableExperienceMetadata metadata = world.Get<PortableExperienceMetadata>(portableExperienceEntity);

                switch (metadata.Type)
                {
                    // No SmartWearable case: its running state lives in SmartWearableCache, updated through the PortableExperienceUnloaded event raised below.
                    case PortableExperienceType.Local:
                        localPortableExperienceCache.RunningPortableExperiences.Remove(id);
                        break;
                    case PortableExperienceType.Global:
                        globalPortableExperienceCache.RunningPortableExperiences.Remove(id);
                        break;
                }

                if (metadata.Type == PortableExperienceType.Local &&
                    localPortableExperiencesPerScene.TryGetValue(metadata.ParentSceneId, out int count))
                {
                    if (count <= 1) localPortableExperiencesPerScene.Remove(metadata.ParentSceneId);
                    else localPortableExperiencesPerScene[metadata.ParentSceneId] = count - 1;
                }

                world.Add<DeleteEntityIntention>(portableExperienceEntity);

                PortableExperienceEntities.Remove(id);

                PortableExperienceUnloaded?.Invoke(id);

                return new IPortableExperiencesController.ExitResponse { status = true };
            }

            return new IPortableExperiencesController.ExitResponse { status = false };
        }

        public IPortableExperiencesController.ExitResponse KillPortableExperienceById(string id)
        {
            if (!PortableExperienceEntities.TryGetValue(id, out Entity portableExperienceEntity))
                return new IPortableExperiencesController.ExitResponse { status = false };

            PortableExperienceType type = world.Get<PortableExperienceMetadata>(portableExperienceEntity).Type;

            IPortableExperiencesController.ExitResponse response = UnloadPortableExperienceById(id);

            if (response.status)
                switch (type)
                {
                    case PortableExperienceType.SmartWearable:
                        smartWearableCache.KilledPortableExperiences.Add(id);
                        break;
                    case PortableExperienceType.Local:
                        localPortableExperienceCache.KilledPortableExperiences.Add(id);
                        break;
                    case PortableExperienceType.Global:
                        globalPortableExperienceCache.KilledPortableExperiences.Add(id);
                        break;
                }

            return response;
        }

        public void KillPortableExperience(string id) =>
            KillPortableExperienceById(id);

        public void UnloadPortableExperience(string id) =>
            UnloadPortableExperienceById(id);
    }
}
