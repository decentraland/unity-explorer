using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin
    {
        private readonly IAnalyticsReportProfiler profiler;
        private readonly IRealmNavigator realmNavigator;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly WalkedDistanceAnalytics walkedDistanceAnalytics;

        public AnalyticsPlugin(IAnalyticsController analytics, IAnalyticsReportProfiler profiler, IRealmNavigator realmNavigator, IRealmData realmData, IScenesCache scenesCache,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, IWeb3IdentityCache identityCache, IDebugContainerBuilder debugContainerBuilder)
        {
            this.analytics = analytics;

            this.profiler = profiler;
            this.realmNavigator = realmNavigator;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.identityCache = identityCache;
            this.debugContainerBuilder = debugContainerBuilder;

            walkedDistanceAnalytics = new WalkedDistanceAnalytics(analytics, mainPlayerAvatarBaseProxy);
            this.realmNavigator.RealmChanged += OnRealmChanged;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            walkedDistanceAnalytics.Initialize();

            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, profiler, arguments.V8ActiveEngines, scenesCache);
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
            MoveMetricsSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity, identityCache, debugContainerBuilder, walkedDistanceAnalytics);
            AnalyticsEmotesSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
        }

        public void Dispose()
        {
            walkedDistanceAnalytics.Dispose();
            this.realmNavigator.RealmChanged -= OnRealmChanged;

        }

        private void OnRealmChanged(bool _) =>
            analytics.Flush();

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
