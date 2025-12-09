using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.WebRequests;
using ECS;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public class ProfilesContainer
    {
        public readonly IProfileCache Cache;

        private readonly IWebRequestController webRequestController;
        private readonly RealmProfileRepository repository;
        private readonly ProfilesDebug profilesDebug;

        public IProfileRepository Repository { get; }

        public ProfilesContainer(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, IRealmData realmData, IDebugContainerBuilder debugContainerBuilder)
        {
            this.webRequestController = webRequestController;
            Cache = new DefaultProfileCache();

            profilesDebug = ProfilesDebug.Create(debugContainerBuilder);

            Repository = new LogProfileRepository(
                repository = new RealmProfileRepository(webRequestController, realmData, urlsSource, Cache, profilesDebug,

                    // TODO remove hardcode
                    true || FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.Endpoints.USE_CENTRALIZED_PROFILES))
            );
        }

        public ProfilesPlugin CreatePlugin() =>
            new (repository, webRequestController, profilesDebug);

        public class ProfilesPlugin : IDCLGlobalPlugin<ProfilesPlugin.Settings>
        {
            private readonly RealmProfileRepository repository;
            private readonly IWebRequestController webRequestController;
            private readonly ProfilesDebug profilesDebug;

            private TimeSpan heartbeat;

            public ProfilesPlugin(RealmProfileRepository repository, IWebRequestController webRequestController, ProfilesDebug profilesDebug)
            {
                this.repository = repository;
                this.webRequestController = webRequestController;
                this.profilesDebug = profilesDebug;
            }

            public void Dispose() { }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
            {
                LoadProfilesBatchSystem.InjectToWorld(ref builder, repository, webRequestController, profilesDebug);
                PrepareProfilesBatchSystem.InjectToWorld(ref builder, heartbeat, repository);
            }

            public UniTask InitializeAsync(Settings settings, CancellationToken ct)
            {
                heartbeat = TimeSpan.FromMilliseconds(settings.batchHeartbeatMs);
                return UniTask.CompletedTask;
            }

            [Serializable]
            public class Settings : IDCLPluginSettings
            {
                [field: SerializeField]
                internal uint batchHeartbeatMs { get; private set; } = 100;
            }
        }
    }
}
