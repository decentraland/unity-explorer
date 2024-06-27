using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Chat;
using DCL.ExplorePanel;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin<AnalyticsSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IProfilingProvider profilingProvider;
        private readonly IRealmData realmData;
        private readonly ICharacterObject characterObject;
        private readonly IScenesCache scenesCache;
        private readonly MVCManager mvcManager;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IWeb3IdentityCache identityCache;

        private AnalyticsController analytics;
        private AnalyticsConfiguration analyticsConfig;

        public AnalyticsPlugin(IAssetsProvisioner assetsProvisioner, IProfilingProvider profilingProvider, IRealmData realmData,
            ICharacterObject characterObject, IScenesCache scenesCache,
            MVCManager mvcManager, IChatMessagesBus chatMessagesBus, IWeb3IdentityCache identityCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.profilingProvider = profilingProvider;
            this.realmData = realmData;
            this.characterObject = characterObject;
            this.scenesCache = scenesCache;
            this.mvcManager = mvcManager;
            this.chatMessagesBus = chatMessagesBus;
            this.identityCache = identityCache;
        }

        public async UniTask InitializeAsync(AnalyticsSettings settings, CancellationToken ct)
        {
            analyticsConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.AnalyticsConfigRef, ct)).Value;

            analytics = new AnalyticsController(
                new DebugAnalyticsService(),
                // new SegmentAnalyticsService(analyticsConfig),
                realmData, characterObject.Transform, identityCache
                );

            mvcManager.ControllerRegistered += OnControllerRegistered;
        }

        public void Dispose()
        {
            mvcManager.ControllerRegistered -= OnControllerRegistered;
        }

        private void OnControllerRegistered(IController controller)
        {
            switch (controller)
            {
                case ChatController chatController:
                {
                    var chatAnalytics = new ChatAnalytics(analytics, chatController, chatMessagesBus);
                    break;
                }
                case ExplorePanelController explorePanelController:
                {
                    var mapAnalytics = new MapAnalytics(analytics, explorePanelController.NavmapController);
                    break;
                }
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            // WalkedDistanceAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
            // PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, analyticsConfig, profilingProvider);
            // TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
        }
    }

    [Serializable]
    public class AnalyticsSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AnalyticsConfigurationRef AnalyticsConfigRef;

        [Serializable]
        public class AnalyticsConfigurationRef : AssetReferenceT<AnalyticsConfiguration>
        {
            public AnalyticsConfigurationRef(string guid) : base(guid) { }
        }
    }
}
