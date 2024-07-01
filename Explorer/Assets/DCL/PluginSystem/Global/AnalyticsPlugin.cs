using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.ExplorePanel;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using ECS;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly MVCManager mvcManager;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IChatCommand teleportToCommand;

        private readonly AnalyticsController analytics;
        private readonly AnalyticsConfiguration analyticsConfig;

        public AnalyticsPlugin(AnalyticsController analytics,  AnalyticsConfiguration analyticsConfig,
            IProfilingProvider profilingProvider, IRealmData realmData, IScenesCache scenesCache,
                                                                                                                                                                               MVCManager mvcManager, IChatMessagesBus chatMessagesBus, IChatCommand teleportToCommand)
        {
            this.analytics = analytics;
            this.profilingProvider = profilingProvider;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.mvcManager = mvcManager;
            this.chatMessagesBus = chatMessagesBus;
            this.teleportToCommand = teleportToCommand;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            mvcManager.ControllerRegistered += OnControllerRegistered;
            return UniTask.CompletedTask;
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
                    var chatAnalytics = new ChatAnalytics(analytics, chatController, chatMessagesBus, teleportToCommand);
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
            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            WalkedDistanceAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, analyticsConfig, profilingProvider);
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
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
