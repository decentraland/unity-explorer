using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
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

        public IProfileRepository Repository { get; }

        public ProfilesContainer(IWebRequestController webRequestController, IRealmData realmData)
        {
            this.webRequestController = webRequestController;
            Cache = new DefaultProfileCache();

            Repository = new LogProfileRepository(
                repository = new RealmProfileRepository(webRequestController, realmData, Cache)
            );
        }

        public Plugin CreatePlugin() =>
            new (repository, webRequestController);

        public class Plugin : IDCLGlobalPlugin<Plugin.Settings>
        {
            private readonly RealmProfileRepository repository;
            private readonly IWebRequestController webRequestController;

            private TimeSpan heartbeat;

            public Plugin(RealmProfileRepository repository, IWebRequestController webRequestController)
            {
                this.repository = repository;
                this.webRequestController = webRequestController;
            }

            public void Dispose() { }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
            {
                LoadProfilesBatchSystem.InjectToWorld(ref builder, repository, webRequestController);
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
